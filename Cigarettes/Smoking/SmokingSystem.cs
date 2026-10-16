using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Cigarettes.Smoking;

/// <summary>
/// Server-side bookkeeping for nicotine: the timed buff from a puff, the player's
/// nicotine level, its slow decay, and a mild withdrawal once a habit has formed
/// (small hunger and mining penalties plus melodramatic chat lines; the level Ata picked).
/// </summary>
public class SmokingSystem : ModSystem
{
    const string BuffCode = "cigarettes-nicotine";
    const string WithdrawalCode = "cigarettes-withdrawal";
    const string NicotineAttr = "cigarettesNicotine";
    const string WithdrawalAttr = "cigarettesWithdrawal";

    // Tuning. Withdrawal kicks in after roughly 30 puffs in a short time and fades as nicotine decays.
    const float MaxNicotine = 100f;
    const float DecayPerMinute = 0.5f;
    const float WithdrawalThreshold = 30f;
    const float CoughThreshold = 50f;
    const int CoughLines = 6;
    const int CravingLines = 5;
    static readonly Dictionary<string, float> WithdrawalEffects = new()
    {
        ["hungerrate"] = 0.15f,
        ["miningSpeedMul"] = -0.05f,
    };

    ICoreServerAPI? sapi;
    readonly Dictionary<long, ActiveBuff> buffs = new();

    record ActiveBuff(EntityAgent Entity, long ExpiresAtMs, string[] Stats);

    public bool IsBuffed(long entityId) => buffs.ContainsKey(entityId);

    AchievementSystem? Achievements => sapi?.ModLoader.GetModSystem<AchievementSystem>();

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        api.Event.RegisterGameTickListener(OnTick, 5000);
    }

    /// <summary>Applies (or refreshes) the buff of one puff and adds nicotine.</summary>
    public void ApplyPuff(EntityAgent entity, IReadOnlyDictionary<string, float> effects, float strength, int durationSec)
    {
        if (sapi == null) return;

        foreach (var (stat, value) in effects)
        {
            entity.Stats.Set(stat, BuffCode, value * strength, false);
        }
        buffs[entity.EntityId] = new ActiveBuff(entity, sapi.World.ElapsedMilliseconds + durationSec * 1000L, effects.Keys.ToArray());

        float nicotine = GameMath.Min(entity.WatchedAttributes.GetFloat(NicotineAttr) + strength, MaxNicotine);
        entity.WatchedAttributes.SetFloat(NicotineAttr, nicotine);
        SetWithdrawal(entity, false);
        Achievements?.OnNicotineChanged(entity, nicotine);
    }

    void OnTick(float dt)
    {
        if (sapi == null) return;
        long now = sapi.World.ElapsedMilliseconds;

        foreach (var (id, buff) in buffs.Where(b => b.Value.ExpiresAtMs <= now).ToList())
        {
            foreach (string stat in buff.Stats) buff.Entity.Stats.Remove(stat, BuffCode);
            buffs.Remove(id);
        }

        foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
        {
            EntityAgent? entity = player.Entity;
            if (entity == null) continue;

            float nicotine = entity.WatchedAttributes.GetFloat(NicotineAttr);
            if (nicotine <= 0) continue;

            nicotine = GameMath.Max(0, nicotine - DecayPerMinute * dt / 60f);
            entity.WatchedAttributes.SetFloat(NicotineAttr, nicotine);
            Achievements?.OnNicotineChanged(entity, nicotine);

            // Heavy smokers cough now and then, about once every 8 minutes above the threshold
            if (nicotine >= CoughThreshold && sapi.World.Rand.NextDouble() < 0.01)
            {
                int line = sapi.World.Rand.Next(CoughLines);
                player.SendMessage(GlobalConstants.InfoLogChatGroup,
                    Lang.GetL(player.LanguageCode, "cigarettes:cough-" + line), EnumChatType.Notification);
                Achievements?.OnCough(entity);
            }

            // While craving, the occasional melodramatic reminder (about every 10 minutes)
            if (entity.WatchedAttributes.GetBool(WithdrawalAttr) && sapi.World.Rand.NextDouble() < 0.008)
            {
                int line = sapi.World.Rand.Next(CravingLines);
                player.SendMessage(GlobalConstants.InfoLogChatGroup,
                    Lang.GetL(player.LanguageCode, "cigarettes:craving-" + line), EnumChatType.Notification);
            }

            bool craving = nicotine >= WithdrawalThreshold && !buffs.ContainsKey(entity.EntityId);
            if (SetWithdrawal(entity, craving) && craving)
            {
                Achievements?.OnWithdrawal(entity);
                player.SendMessage(GlobalConstants.InfoLogChatGroup,
                    Lang.GetL(player.LanguageCode, "cigarettes:withdrawal-start"), EnumChatType.Notification);
            }
        }
    }

    /// <summary>Turns withdrawal on or off. Returns true if it changed.</summary>
    static bool SetWithdrawal(EntityAgent entity, bool on)
    {
        if (entity.WatchedAttributes.GetBool(WithdrawalAttr) == on) return false;

        foreach (var (stat, value) in WithdrawalEffects)
        {
            if (on) entity.Stats.Set(stat, WithdrawalCode, value, false);
            else entity.Stats.Remove(stat, WithdrawalCode);
        }
        entity.WatchedAttributes.SetBool(WithdrawalAttr, on);
        return true;
    }
}
