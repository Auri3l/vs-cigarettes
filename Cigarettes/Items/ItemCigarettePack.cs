using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Cigarettes.Items;

/// <summary>
/// A branded pack of cigarettes. Right click to shake one out.
/// </summary>
public class ItemCigarettePack : Item
{
    const string CountAttr = "cigarettesLeft";

    int packSize = 20;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        packSize = Attributes?["packSize"].AsInt(packSize) ?? packSize;
    }

    int Count(ItemStack stack) => stack.Attributes.GetInt(CountAttr, packSize);

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!firstEvent || slot.Itemstack == null) return;
        handling = EnumHandHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server) return;

        Item? cigarette = byEntity.World.GetItem(CodeWithPath("cigarette-" + Variant["brand"]));
        if (cigarette == null) return;

        var one = new ItemStack(cigarette);
        if (!byEntity.TryGiveItemStack(one)) byEntity.World.SpawnItemEntity(one, byEntity.Pos.XYZ);

        int left = Count(slot.Itemstack) - 1;
        byEntity.Api.ModLoader.GetModSystem<Smoking.AchievementSystem>()
            ?.OnPackOpened(byEntity, Variant["brand"], Attributes?["warning"].AsInt(0) ?? 0, left <= 0);
        if (left <= 0)
        {
            slot.TakeOut(1);
            slot.MarkDirty();
            return;
        }

        // An opened pack no longer stacks with sealed ones: keep one opened pack in hand
        // (its count changes first, so the sealed rest can't merge back into this slot)
        ItemStack? sealedRest = null;
        if (slot.Itemstack.StackSize > 1)
        {
            sealedRest = slot.Itemstack.Clone();
            sealedRest.StackSize--;
            slot.Itemstack.StackSize = 1;
        }
        slot.Itemstack.Attributes.SetInt(CountAttr, left);
        slot.MarkDirty();

        if (sealedRest != null && !byEntity.TryGiveItemStack(sealedRest))
        {
            byEntity.World.SpawnItemEntity(sealedRest, byEntity.Pos.XYZ);
        }
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
        if (inSlot.Itemstack == null) return;

        int count = Count(inSlot.Itemstack);
        dsc.AppendLine(count == packSize
            ? Lang.Get("cigarettes:pack-sealed", count)
            : Lang.Get("cigarettes:pack-count", count, packSize));
        dsc.AppendLine("<i>" + Lang.Get("cigarettes:slogan-" + Variant["brand"]) + "</i>");

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
            }
        }.Append(base.GetHeldInteractionHelp(inSlot));
    }
}
