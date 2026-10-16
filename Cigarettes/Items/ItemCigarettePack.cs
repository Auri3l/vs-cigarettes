using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Cigarettes.Items;

/// <summary>
/// A branded pack of cigarettes. Right click to shake one out. An empty pack stays in your hand,
/// and right clicking with cigarettes in the offhand puts them (any kind) back in.
/// A sealed pack has no contents attribute and holds packSize of its own brand.
/// </summary>
public class ItemCigarettePack : Item
{
    const string ContentsAttr = "contents";
    // Packs opened before contents were tracked only stored how many were left
    const string LegacyCountAttr = "cigarettesLeft";

    int packSize = 20;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        packSize = Attributes?["packSize"].AsInt(packSize) ?? packSize;
    }

    static bool IsSealed(ItemStack pack) => pack.Attributes[ContentsAttr] == null && !pack.Attributes.HasAttribute(LegacyCountAttr);

    /// <summary>What's inside, in the order it went in (the last one comes out first).</summary>
    List<ItemStack> Contents(ItemStack pack, IWorldAccessor world)
    {
        var list = new List<ItemStack>();
        if (pack.Attributes[ContentsAttr] is ITreeAttribute tree)
        {
            for (int i = 0; i < tree.Count; i++)
            {
                ItemStack? s = tree.GetItemstack(i.ToString());
                if (s == null) continue;
                s.ResolveBlockOrItem(world);
                if (s.Collectible != null) list.Add(s);
            }
            return list;
        }

        int count = pack.Attributes.GetInt(LegacyCountAttr, packSize);
        if (count > 0 && world.GetItem(CodeWithPath("cigarette-" + Variant["brand"])) is Item own)
        {
            list.Add(new ItemStack(own, count));
        }
        return list;
    }

    static void SetContents(ItemStack pack, List<ItemStack> contents)
    {
        var tree = new TreeAttribute();
        for (int i = 0; i < contents.Count; i++) tree.SetItemstack(i.ToString(), contents[i]);
        pack.Attributes[ContentsAttr] = tree;
        pack.Attributes.RemoveAttribute(LegacyCountAttr);
    }

    /// <summary>Only whole, unlit cigarettes go back in a pack.</summary>
    static bool CanGoInPack(ItemStack? stack)
    {
        return stack?.Collectible is ItemCigarette && stack.Collectible.FirstCodePart() == "cigarette"
            && !stack.Attributes.GetBool("lit") && !stack.Attributes.HasAttribute("puffsLeft");
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!firstEvent || slot.Itemstack == null) return;

        // Sneak + right click on a block places it as ground storage (CollectibleBehaviorGroundStorable)
        if (byEntity.Controls.ShiftKey && blockSel != null)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }
        handling = EnumHandHandling.PreventDefault;

        ItemSlot? offhand = byEntity.LeftHandItemSlot;
        bool filling = CanGoInPack(offhand?.Itemstack);
        if (filling && Contents(slot.Itemstack, byEntity.World).Sum(s => s.StackSize) >= packSize)
        {
            (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, "packfull", Lang.Get("cigarettes:ingameerror-packfull"));
            return;
        }
        if (byEntity.World.Side != EnumAppSide.Server) return;

        // Opening or filling one pack of a stack: keep that one in hand and give the rest back
        // (its contents change first, so the rest can't merge back into this slot)
        ItemStack? rest = null;
        if (slot.Itemstack.StackSize > 1)
        {
            rest = slot.Itemstack.Clone();
            rest.StackSize--;
            slot.Itemstack.StackSize = 1;
        }

        if (filling) Fill(slot.Itemstack, offhand!, byEntity);
        else TakeOne(slot.Itemstack, byEntity);
        slot.MarkDirty();

        if (rest != null && !byEntity.TryGiveItemStack(rest))
        {
            byEntity.World.SpawnItemEntity(rest, byEntity.Pos.XYZ);
        }
    }

    void TakeOne(ItemStack pack, EntityAgent byEntity)
    {
        List<ItemStack> contents = Contents(pack, byEntity.World);
        if (contents.Count == 0) return;

        ItemStack last = contents[^1];
        ItemStack one = last.Clone();
        one.StackSize = 1;
        if (--last.StackSize <= 0) contents.RemoveAt(contents.Count - 1);
        SetContents(pack, contents);

        if (!byEntity.TryGiveItemStack(one)) byEntity.World.SpawnItemEntity(one, byEntity.Pos.XYZ);

        int left = contents.Sum(s => s.StackSize);
        byEntity.Api.ModLoader.GetModSystem<Smoking.AchievementSystem>()
            ?.OnPackOpened(byEntity, Variant["brand"], Attributes?["warning"].AsInt(0) ?? 0, left <= 0);
    }

    void Fill(ItemStack pack, ItemSlot from, EntityAgent byEntity)
    {
        IWorldAccessor world = byEntity.World;
        List<ItemStack> contents = Contents(pack, world);
        int room = packSize - contents.Sum(s => s.StackSize);
        if (room <= 0 || from.Itemstack == null) return;

        ItemStack moved = from.TakeOut(System.Math.Min(room, from.StackSize));
        from.MarkDirty();

        // Merge with the top of the pile if it's the same kind, so the order things come out in is kept
        if (contents.Count > 0 && contents[^1].Equals(world, moved, GlobalConstants.IgnoredStackAttributes)) contents[^1].StackSize += moved.StackSize;
        else contents.Add(moved);
        SetContents(pack, contents);
    }

    /// <summary>A rotting pack that rots away in a player's inventory counts for the "Best Before" achievement.</summary>
    public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
    {
        if (props.Type == EnumTransitionType.Perish && Attributes?["rotting"].AsBool() == true
            && api.Side == EnumAppSide.Server && slot.Inventory is InventoryBasePlayer inv && inv.Player?.Entity is EntityAgent owner)
        {
            api.ModLoader.GetModSystem<Smoking.AchievementSystem>()?.OnPackRotted(owner);
        }
        return base.OnTransitionNow(slot, props);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        ItemStack? pack = inSlot.Itemstack;
        if (pack == null) return;

        List<ItemStack> contents = Contents(pack, world);
        int count = contents.Sum(s => s.StackSize);
        if (IsSealed(pack)) dsc.AppendLine(Lang.Get("cigarettes:pack-sealed", count));
        else if (count == 0) dsc.AppendLine(Lang.Get("cigarettes:pack-empty"));
        else dsc.AppendLine(Lang.Get("cigarettes:pack-count", count, packSize));

        // The brand's own blend, plus anything else that has been put in since
        string brand = Variant["brand"];
        if (Lang.HasTranslation("cigarettes:blend-" + brand)) dsc.AppendLine(Lang.Get("cigarettes:blend-label", Lang.Get("cigarettes:blend-" + brand)));
        if (contents.Any(s => s.Collectible.Variant["brand"] != brand || s.Attributes.HasAttribute(ItemCigarette.TobaccoAttr)))
        {
            string inside = string.Join(", ", contents.Select(s => s.StackSize + "x " + s.GetName()));
            dsc.AppendLine(Lang.Get("cigarettes:pack-contents", inside));
        }

        dsc.AppendLine("<i>" + Lang.Get("cigarettes:slogan-" + brand) + "</i>");

        // Every brand carries its own government-mandated warning (pack attribute "warning")
        int warning = Attributes?["warning"].AsInt(0) ?? 0;
        dsc.AppendLine("<font color=\"#ff8866\">" + Lang.Get("cigarettes:warning-label", Lang.Get("cigarettes:warning-" + warning)) + "</font>");
        if (Attributes?["rotting"].AsBool() == true) dsc.AppendLine(Lang.Get("cigarettes:pack-rotting"));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction
            {
                ActionLangCode = "cigarettes:heldhelp-takecigarette",
                MouseButton = EnumMouseButton.Right,
            },
            new WorldInteraction
            {
                ActionLangCode = "cigarettes:heldhelp-fillpack",
                MouseButton = EnumMouseButton.Right,
            }
        }.Append(base.GetHeldInteractionHelp(inSlot));
    }
}
