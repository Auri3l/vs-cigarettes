using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Cigarettes.Smoking;

/// <summary>
/// The Smoker's Ledger: a deliberately over-engineered achievement system.
///
/// Gameplay code reports events (a puff, a light, a pack opened, a cough, a death...).
/// Events bump named counters and fill named sets on the player's record. Achievements,
/// defined in assets/cigarettes/config/achievements.json, unlock when a counter reaches a target,
/// a set reaches a size, or enough other achievements are unlocked, but only once all their
/// prerequisites are unlocked. Each achievement is worth points by tier; points earn ranks,
/// rank-ups are announced to the whole server, and some ranks hand out rewards.
/// Hidden achievements show only a hint until their prerequisites are done.
/// Some achievements grant a permanent character trait (defined in config/traits.json, which the game merges with its own).
/// </summary>
public class AchievementSystem : ModSystem
{
    const string RecordAttr = "cigarettesRecord";
    const int AllOfThem = 0;

    static readonly HashSet<string> MentholBrands = new() { "menthol", "newpart", "koal", "vagueslims" };
    static readonly HashSet<string> KretekBrands = new() { "kretek", "djarun" };
    static readonly string[] Kinds = { "cigarette", "cigar", "stubcigar", "pipe", "vape" };

    ICoreServerAPI? sapi;
    LedgerConfig config = new();
    Dictionary<string, int> setSizes = new();

    public class AchievementDef
    {
        public string Code = "";
        public string Category = "general";
        public string Tier = "bronze";
        public bool Hidden;
        public string[] Requires = Array.Empty<string>();
        public string? Counter;
        public double Target = 1;
        public string? Set;
        public int SetSize;
        public int MetaCount;
        public string? Trait;
    }

    public class RankDef
    {
        public int Points;
        public string Code = "";
        public JsonItemStack? Reward;
    }

    public class LedgerConfig
    {
        public Dictionary<string, int> TierPoints = new();
        public RankDef[] Ranks = Array.Empty<RankDef>();
        public AchievementDef[] Achievements = Array.Empty<AchievementDef>();
    }

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        api.Event.PlayerDeath += OnPlayerDeath;

        var parsers = api.ChatCommands.Parsers;
        api.ChatCommands.Create("smokestats")
            .WithDescription(Lang.Get("cigarettes:smokestats-desc"))
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(args => TextCommandResult.Success(Summary((IServerPlayer)args.Caller.Player)))
            .BeginSubCommand("list")
                .WithDescription(Lang.Get("cigarettes:smokestats-list-desc"))
                .WithArgs(parsers.OptionalWord("category"))
                .HandleWith(args => TextCommandResult.Success(List((IServerPlayer)args.Caller.Player, args[0] as string)))
            .EndSubCommand()
            .BeginSubCommand("ranks")
                .WithDescription(Lang.Get("cigarettes:smokestats-ranks-desc"))
                .HandleWith(args => TextCommandResult.Success(Ranks((IServerPlayer)args.Caller.Player)))
            .EndSubCommand();
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server) return;

        config = api.Assets.Get(new AssetLocation(CigarettesModSystem.ModId, "config/achievements.json"))
            .ToObject<LedgerConfig>();

        foreach (RankDef rank in config.Ranks)
        {
            rank.Reward?.Resolve(api.World, "cigarettes rank reward");
        }

        // "All of them" sizes for sets, worked out from what's actually loaded
        string[] VariantsOf(string firstPart, string group) => api.World.Items
            .Where(i => i.Code != null && i.Code.Domain == CigarettesModSystem.ModId && i.FirstCodePart() == firstPart)
            .Select(i => i.Variant[group]).Distinct().ToArray();

        setSizes = new Dictionary<string, int>
        {
            ["brands"] = VariantsOf("cigarette", "brand").Length,
            ["packbrands"] = VariantsOf("cigarettepack", "brand").Length,
            ["kinds"] = Kinds.Length,
        };
    }

    // ---------------------------------------------------------------- events from gameplay

    public void OnPuff(EntityAgent entity, CollectibleObject smoked)
    {
        if (sapi == null) return;
        ITreeAttribute record = Record(entity);

        string kind = smoked.FirstCodePart();
        Bump(record, "puffs");
        Bump(record, "puffs:" + kind);
        AddToSet(record, "kinds", kind);

        if (kind == "cigarette")
        {
            string brand = smoked.Variant["brand"];
            Bump(record, "puffs:brand:" + brand);
            AddToSet(record, "brands", brand);
            if (MentholBrands.Contains(brand)) Bump(record, "puffs:menthol");
            if (KretekBrands.Contains(brand)) Bump(record, "puffs:kretek");
        }
        if (kind == "vape") Bump(record, "puffs:vape");

        foreach (string context in Contexts(entity))
        {
            Bump(record, "puffs:ctx:" + context);
            AddToSet(record, "contexts", context);
        }

        TrackStreak(record);
        TrackTastingMenu(record, kind);
        Evaluate(entity);
    }

    public void OnSmokedOut(EntityAgent entity, CollectibleObject smoked)
    {
        Bump(Record(entity), "finished:" + smoked.FirstCodePart());
        Evaluate(entity);
    }

    /// <summary>source is "chain" (from a lit smokable), "light" (torch, lantern...), "matches" or "lighter".</summary>
    public void OnLit(EntityAgent entity, string source)
    {
        Bump(Record(entity), "lights:" + source);
        Evaluate(entity);
    }

    public void OnPackOpened(EntityAgent entity, string brand, int warning, bool emptied)
    {
        ITreeAttribute record = Record(entity);
        Bump(record, "packsopened");
        if (emptied) Bump(record, "packsemptied");
        AddToSet(record, "packbrands", brand);
        AddToSet(record, "warnings", warning.ToString());
        Evaluate(entity);
    }

    public void OnBatch(EntityAgent entity, string kind) { Bump(Record(entity), "batches:" + kind); Evaluate(entity); }

    /// <summary>where is "ground" (landed as ground storage), "ashtray", or "lost" (water, no room...)</summary>
    public void OnFlick(EntityAgent entity, string where)
    {
        ITreeAttribute record = Record(entity);
        Bump(record, "flicks");
        Bump(record, "flicks:" + where);
        Evaluate(entity);
    }

    public void OnPackRotted(EntityAgent entity) { Bump(Record(entity), "rotpacksrotted"); Evaluate(entity); }

    public void OnTemporalCharge(EntityAgent entity) { Bump(Record(entity), "temporalcharges"); Evaluate(entity); }
    public void OnAshtrayFull(EntityAgent entity) { Bump(Record(entity), "ashtraysfilled"); Evaluate(entity); }
    public void OnCough(EntityAgent entity) { Bump(Record(entity), "coughs"); Evaluate(entity); }

    public void OnWithdrawal(EntityAgent entity)
    {
        ITreeAttribute record = Record(entity);
        Bump(record, "withdrawals");
        record.SetBool("inWithdrawalCycle", true);
        Evaluate(entity);
    }

    public void OnNicotineChanged(EntityAgent entity, float nicotine)
    {
        ITreeAttribute record = Record(entity);
        bool changed = false;
        if (nicotine >= 100 && Counter(record, "maxnicotine") < 1) { Bump(record, "maxnicotine"); changed = true; }

        // Quitting counts only after riding a withdrawal all the way down to zero
        if (nicotine <= 0 && record.GetBool("inWithdrawalCycle"))
        {
            record.SetBool("inWithdrawalCycle", false);
            Bump(record, "quit");
            changed = true;
        }
        if (changed) Evaluate(entity);
    }

    void OnPlayerDeath(IServerPlayer player, DamageSource damageSource)
    {
        SmokingSystem? smoking = sapi?.ModLoader.GetModSystem<SmokingSystem>();
        if (player.Entity == null || smoking == null || !smoking.IsBuffed(player.Entity.EntityId)) return;
        Bump(Record(player.Entity), "deaths:smoking");
        Evaluate(player.Entity);
    }

    // ---------------------------------------------------------------- situations

    /// <summary>Every situation the smoker is in right now, for the situational achievements.</summary>
    List<string> Contexts(EntityAgent entity)
    {
        var contexts = new List<string>();
        IWorldAccessor world = sapi!.World;
        BlockPos pos = entity.Pos.AsBlockPos;

        double hour = world.Calendar.HourOfDay;
        if (hour < 5 || hour >= 21) contexts.Add("night");

        bool outside = world.BlockAccessor.GetRainMapHeightAt(pos) <= pos.Y;
        ClimateCondition? climate = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
        if (climate != null)
        {
            if (outside && climate.Rainfall > 0.1f) contexts.Add("rain");
            if (climate.Temperature < -10) contexts.Add("cold");
            if (climate.Temperature > 35) contexts.Add("hot");
        }

        if (sapi.ModLoader.GetModSystem<SystemTemporalStability>()?.StormData?.nowStormActive == true) contexts.Add("storm");
        if (!outside && pos.Y < world.SeaLevel - 10) contexts.Add("underground");
        if (pos.Y > world.SeaLevel + 100) contexts.Add("altitude");

        if (entity.Swimming) contexts.Add("swimming");
        if (entity.IsOnFire) contexts.Add("onfire");
        if (entity.Controls.Sprint) contexts.Add("sprinting");
        if (entity.MountedOn != null) contexts.Add("mounted");

        ITreeAttribute? health = entity.WatchedAttributes.GetTreeAttribute("health");
        if (health != null && health.GetFloat("currenthealth") <= 2) contexts.Add("lowhealth");
        ITreeAttribute? hunger = entity.WatchedAttributes.GetTreeAttribute("hunger");
        if (hunger != null && hunger.GetFloat("currentsaturation") <= 0) contexts.Add("hungry");

        Vec3d at = entity.Pos.XYZ;
        if (world.GetEntitiesAround(at, 10, 5, e => e.Code.Path.StartsWith("humanoid-trader")).Length > 0) contexts.Add("neartrader");

        SmokingSystem? smoking = sapi.ModLoader.GetModSystem<SmokingSystem>();
        if (smoking != null && world.GetEntitiesAround(at, 8, 4,
                e => e is EntityPlayer && e.EntityId != entity.EntityId && smoking.IsBuffed(e.EntityId)).Length > 0)
        {
            contexts.Add("nearsmoker");
        }

        return contexts;
    }

    /// <summary>Consecutive in-game days with at least one puff.</summary>
    void TrackStreak(ITreeAttribute record)
    {
        int today = (int)sapi!.World.Calendar.TotalDays;
        int lastDay = record.GetInt("lastSmokeDay", int.MinValue);
        if (today == lastDay) return;

        int streak = today == lastDay + 1 ? record.GetInt("streak") + 1 : 1;
        record.SetInt("streak", streak);
        record.SetInt("lastSmokeDay", today);
        SetAtLeast(record, "beststreak", streak);
    }

    /// <summary>How many different kinds were smoked within the last in-game hour.</summary>
    void TrackTastingMenu(ITreeAttribute record, string kind)
    {
        double now = sapi!.World.Calendar.TotalHours;
        ITreeAttribute recent = Subtree(record, "recentKinds");
        recent.SetDouble(kind, now);
        int within = Kinds.Count(k => recent.HasAttribute(k) && now - recent.GetDouble(k) <= 1.0);
        SetAtLeast(record, "tastingmenu", within);
    }

    // ---------------------------------------------------------------- evaluation

    void Evaluate(EntityAgent entity)
    {
        ITreeAttribute record = Record(entity);
        HashSet<string> unlocked = GetSet(record, "unlocked");
        int before = Points(unlocked);

        // Unlocking one achievement can satisfy another's prerequisite or meta count, so loop until stable
        bool any;
        do
        {
            any = false;
            foreach (AchievementDef def in config.Achievements)
            {
                if (unlocked.Contains(def.Code) || !def.Requires.All(unlocked.Contains)) continue;
                if (Progress(record, unlocked, def) < Goal(def)) continue;

                unlocked.Add(def.Code);
                Announce(entity, def);
                if (def.Trait != null) GrantTrait(entity, def.Trait);
                any = true;
            }
        } while (any);

        SaveSet(record, "unlocked", unlocked);
        entity.WatchedAttributes.MarkPathDirty(RecordAttr);

        int after = Points(unlocked);
        if (after != before) CheckRankUp(entity, before, after);
    }

    double Progress(ITreeAttribute record, HashSet<string> unlocked, AchievementDef def)
    {
        if (def.Counter != null) return Counter(record, def.Counter);
        if (def.Set != null) return GetSet(record, def.Set).Count;
        if (def.MetaCount != 0) return unlocked.Count(code => config.Achievements.Any(a => a.Code == code && a.MetaCount == 0));
        return 0;
    }

    double Goal(AchievementDef def)
    {
        if (def.Counter != null) return def.Target;
        if (def.Set != null) return def.SetSize != AllOfThem ? def.SetSize : setSizes.GetValueOrDefault(def.Set, int.MaxValue);
        // Meta: a count, or -1 for every non-meta achievement
        if (def.MetaCount != 0) return def.MetaCount > 0 ? def.MetaCount : config.Achievements.Count(a => a.MetaCount == 0);
        return double.MaxValue;
    }

    int Points(HashSet<string> unlocked) => config.Achievements
        .Where(a => unlocked.Contains(a.Code))
        .Sum(a => config.TierPoints.GetValueOrDefault(a.Tier));

    RankDef? RankFor(int points) => config.Ranks.Where(r => r.Points <= points).OrderBy(r => r.Points).LastOrDefault();

    void Announce(EntityAgent entity, AchievementDef def)
    {
        if (entity is not EntityPlayer { Player: IServerPlayer player }) return;

        string lang = player.LanguageCode;
        string msg = Lang.GetL(lang, "cigarettes:achievement-unlocked",
            Lang.GetL(lang, "cigarettes:tier-" + def.Tier),
            Lang.GetL(lang, $"cigarettes:achievement-{def.Code}-title"),
            Lang.GetL(lang, $"cigarettes:achievement-{def.Code}-desc"),
            config.TierPoints.GetValueOrDefault(def.Tier));
        player.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
    }

    /// <summary>
    /// Adds the trait to the player's "extraTraits", which the vanilla character system applies
    /// alongside their class traits and shows in the character screen. The stats are re-applied
    /// right away through the character system (by reflection: the method isn't public);
    /// if that ever fails, they still apply on the next login.
    /// </summary>
    void GrantTrait(EntityAgent entity, string trait)
    {
        if (entity is not EntityPlayer player) return;

        string[] traits = player.WatchedAttributes.GetStringArray("extraTraits") ?? Array.Empty<string>();
        if (traits.Contains(trait)) return;
        player.WatchedAttributes.SetStringArray("extraTraits", traits.Append(trait).ToArray());

        CharacterSystem? characters = sapi?.ModLoader.GetModSystem<CharacterSystem>();
        MethodInfo? apply = typeof(CharacterSystem).GetMethod("applyTraitAttributes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        try
        {
            if (characters != null) apply?.Invoke(characters, new object[] { player });
        }
        catch (Exception e)
        {
            Mod.Logger.Warning("Couldn't apply trait {0} right away, it will apply on next login: {1}", trait, e.Message);
        }

        if (player.Player is IServerPlayer sp)
        {
            string code = trait.Substring(trait.IndexOf('-') + 1);
            sp.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.GetL(sp.LanguageCode, "cigarettes:trait-granted",
                    Lang.GetL(sp.LanguageCode, "cigarettes:traitname-" + code),
                    Lang.GetL(sp.LanguageCode, "cigarettes:traitdesc-" + code)),
                EnumChatType.Notification);
        }
    }

    /// <summary>Rank-ups are public shame: the whole server hears about it, and some come with a reward.</summary>
    void CheckRankUp(EntityAgent entity, int before, int after)
    {
        RankDef? oldRank = RankFor(before), newRank = RankFor(after);
        if (newRank == null || newRank == oldRank || entity is not EntityPlayer { Player: IServerPlayer player }) return;

        sapi!.SendMessageToGroup(GlobalConstants.GeneralChatGroup,
            Lang.Get("cigarettes:rank-promoted", player.PlayerName, Lang.Get("cigarettes:rank-" + newRank.Code)),
            EnumChatType.Notification);

        if (newRank.Reward?.ResolvedItemstack is ItemStack reward)
        {
            ItemStack gift = reward.Clone();
            if (!entity.TryGiveItemStack(gift)) sapi.World.SpawnItemEntity(gift, entity.Pos.XYZ);
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.GetL(player.LanguageCode, "cigarettes:rank-reward", gift.StackSize, gift.GetName()),
                EnumChatType.Notification);
        }
    }

    // ---------------------------------------------------------------- /smokestats

    string Summary(IServerPlayer player)
    {
        ITreeAttribute record = Record(player.Entity);
        HashSet<string> unlocked = GetSet(record, "unlocked");
        string lang = player.LanguageCode;
        int points = Points(unlocked);
        RankDef? rank = RankFor(points);
        RankDef? next = config.Ranks.Where(r => r.Points > points).OrderBy(r => r.Points).FirstOrDefault();
        int puffs = (int)Counter(record, "puffs");

        var sb = new StringBuilder();
        sb.AppendLine(Lang.GetL(lang, "cigarettes:smokestats-header", player.PlayerName));
        sb.AppendLine(Lang.GetL(lang, "cigarettes:smokestats-rank",
            rank == null ? "-" : Lang.GetL(lang, "cigarettes:rank-" + rank.Code), points));
        if (next != null)
        {
            sb.AppendLine(Lang.GetL(lang, "cigarettes:smokestats-nextrank",
                Lang.GetL(lang, "cigarettes:rank-" + next.Code), Bar(points - (rank?.Points ?? 0), next.Points - (rank?.Points ?? 0)), next.Points - points));
        }
        sb.AppendLine(Lang.GetL(lang, "cigarettes:smokestats-line", puffs,
            (int)player.Entity.WatchedAttributes.GetFloat("cigarettesNicotine"),
            GetSet(record, "brands").Count, setSizes.GetValueOrDefault("brands"),
            record.GetInt("streak"), (int)Counter(record, "beststreak")));
        sb.AppendLine(Lang.GetL(lang, "cigarettes:smokestats-achievements", unlocked.Count, config.Achievements.Length));
        string[] earned = config.Achievements.Where(a => a.Trait != null && unlocked.Contains(a.Code))
            .Select(a => Lang.GetL(lang, "cigarettes:traitname-" + a.Trait!.Substring(a.Trait.IndexOf('-') + 1))).ToArray();
        if (earned.Length > 0) sb.AppendLine(Lang.GetL(lang, "cigarettes:smokestats-traits", string.Join(", ", earned)));
        foreach (string category in config.Achievements.Select(a => a.Category).Distinct())
        {
            var inCategory = config.Achievements.Where(a => a.Category == category).ToArray();
            sb.AppendLine($"  {Lang.GetL(lang, "cigarettes:category-" + category)}: {inCategory.Count(a => unlocked.Contains(a.Code))}/{inCategory.Length}");
        }
        // The often quoted "11 minutes per cigarette", at 8 puffs a cigarette
        sb.AppendLine(Lang.GetL(lang, "cigarettes:smokestats-footer", puffs * 11 / 8));
        sb.Append(Lang.GetL(lang, "cigarettes:smokestats-help"));
        return sb.ToString();
    }

    string List(IServerPlayer player, string? category)
    {
        ITreeAttribute record = Record(player.Entity);
        HashSet<string> unlocked = GetSet(record, "unlocked");
        string lang = player.LanguageCode;

        var sb = new StringBuilder();
        foreach (var group in config.Achievements.Where(a => category == null || a.Category == category).GroupBy(a => a.Category))
        {
            sb.AppendLine("<strong>" + Lang.GetL(lang, "cigarettes:category-" + group.Key) + "</strong>");
            foreach (AchievementDef def in group)
            {
                string tier = Lang.GetL(lang, "cigarettes:tier-" + def.Tier);
                bool done = unlocked.Contains(def.Code);
                bool available = def.Requires.All(unlocked.Contains);

                string traitNote = def.Trait != null && !def.Hidden
                    ? " " + Lang.GetL(lang, "cigarettes:smokestats-trait", Lang.GetL(lang, "cigarettes:traitname-" + def.Trait.Substring(def.Trait.IndexOf('-') + 1)))
                    : "";

                if (done)
                {
                    sb.AppendLine($"  ★ [{tier}] {Lang.GetL(lang, $"cigarettes:achievement-{def.Code}-title")}: {Lang.GetL(lang, $"cigarettes:achievement-{def.Code}-desc")}{traitNote}");
                }
                else if (def.Hidden)
                {
                    // Secret: a cryptic hint once the prerequisites are done, nothing at all before
                    sb.AppendLine("  ☆ [???] " + (available
                        ? Lang.GetL(lang, $"cigarettes:achievement-{def.Code}-hint")
                        : Lang.GetL(lang, "cigarettes:smokestats-locked")));
                }
                else if (!available)
                {
                    string needs = string.Join(", ", def.Requires.Where(r => !unlocked.Contains(r))
                        .Select(r => Lang.GetL(lang, $"cigarettes:achievement-{r}-title")));
                    sb.AppendLine($"  🔒 [{tier}] {Lang.GetL(lang, $"cigarettes:achievement-{def.Code}-title")} ({Lang.GetL(lang, "cigarettes:smokestats-requires", needs)})");
                }
                else
                {
                    double progress = Progress(record, unlocked, def), goal = Goal(def);
                    sb.AppendLine($"  ☆ [{tier}] {Lang.GetL(lang, $"cigarettes:achievement-{def.Code}-title")}: {Lang.GetL(lang, $"cigarettes:achievement-{def.Code}-desc")} {Bar(progress, goal)} {(int)Math.Min(progress, goal)}/{(int)goal}{traitNote}");
                }
            }
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : Lang.GetL(lang, "cigarettes:smokestats-nocategory",
            string.Join(", ", config.Achievements.Select(a => a.Category).Distinct()));
    }

    string Ranks(IServerPlayer player)
    {
        string lang = player.LanguageCode;
        int points = Points(GetSet(Record(player.Entity), "unlocked"));
        var sb = new StringBuilder();
        foreach (RankDef rank in config.Ranks.OrderBy(r => r.Points))
        {
            string mark = rank.Points <= points ? "★" : "☆";
            string reward = rank.Reward?.ResolvedItemstack is ItemStack r ? $" ({r.StackSize}× {r.GetName()})" : "";
            sb.AppendLine($"  {mark} {rank.Points,5}  {Lang.GetL(lang, "cigarettes:rank-" + rank.Code)}{reward}");
        }
        return sb.ToString().TrimEnd();
    }

    static string Bar(double value, double max, int width = 10)
    {
        int filled = max <= 0 ? width : (int)Math.Round(width * Math.Clamp(value / max, 0, 1));
        return "[" + new string('#', filled) + new string('-', width - filled) + "]";
    }

    // ---------------------------------------------------------------- record storage

    static ITreeAttribute Record(EntityAgent entity)
    {
        ITreeAttribute? tree = entity.WatchedAttributes.GetTreeAttribute(RecordAttr);
        if (tree == null)
        {
            tree = new TreeAttribute();
            entity.WatchedAttributes[RecordAttr] = tree;
        }
        return tree;
    }

    static ITreeAttribute Subtree(ITreeAttribute parent, string key)
    {
        ITreeAttribute? tree = parent.GetTreeAttribute(key);
        if (tree == null)
        {
            tree = new TreeAttribute();
            parent[key] = tree;
        }
        return tree;
    }

    static double Counter(ITreeAttribute record, string name) => Subtree(record, "counters").GetDouble(name);

    static void Bump(ITreeAttribute record, string name, double by = 1)
    {
        ITreeAttribute counters = Subtree(record, "counters");
        counters.SetDouble(name, counters.GetDouble(name) + by);
    }

    static void SetAtLeast(ITreeAttribute record, string name, double value)
    {
        ITreeAttribute counters = Subtree(record, "counters");
        if (counters.GetDouble(name) < value) counters.SetDouble(name, value);
    }

    static HashSet<string> GetSet(ITreeAttribute record, string name)
    {
        string csv = Subtree(record, "sets").GetString(name, "");
        return csv.Length == 0 ? new HashSet<string>() : csv.Split(',').ToHashSet();
    }

    static void SaveSet(ITreeAttribute record, string name, HashSet<string> set)
    {
        Subtree(record, "sets").SetString(name, string.Join(",", set));
    }

    static void AddToSet(ITreeAttribute record, string name, string value)
    {
        HashSet<string> set = GetSet(record, name);
        if (set.Add(value)) SaveSet(record, name, set);
    }
}
