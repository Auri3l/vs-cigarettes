using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Cigarettes.Items;

/// <summary>
/// A reusable pipe. Hold shredded tobacco in the offhand and right click to pack it,
/// then light it like a cigarette. The tobacco variety decides the strength.
/// The pipe is kept when the bowl is smoked out.
/// </summary>
public class ItemPipe : ItemSmokable
{
    const string PackedAttr = "packedWith";

    int puffsPerLoad = 15;
    Dictionary<string, float> varietyStrength = new();

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        puffsPerLoad = Attributes?["puffsPerLoad"].AsInt(puffsPerLoad) ?? puffsPerLoad;
        varietyStrength = Attributes?["varietyStrength"].AsObject<Dictionary<string, float>>() ?? varietyStrength;
    }

    static string? PackedWith(ItemStack stack) => stack.Attributes.GetString(PackedAttr);

    protected override int MaxPuffs(ItemStack stack) => puffsPerLoad;

    protected override bool HasSomethingToSmoke(ItemStack stack) => PackedWith(stack) != null;

    protected override float Strength(ItemStack stack)
    {
        string? variety = PackedWith(stack);
        return variety != null && varietyStrength.TryGetValue(variety, out float s) ? s : baseStrength;
    }

    protected override bool TryAlternateInteract(ItemSlot slot, EntityAgent byEntity)
    {
        if (slot.Itemstack == null || HasSomethingToSmoke(slot.Itemstack)) return false;

        ItemSlot? offhand = byEntity.LeftHandItemSlot;
        CollectibleObject? tobacco = offhand?.Itemstack?.Collectible;
        if (offhand == null || tobacco == null || tobacco.Code.Domain != Code.Domain || tobacco.FirstCodePart() != "shreddedtobacco")
        {
            return false;
        }

        if (byEntity.World.Side == EnumAppSide.Server)
        {
            offhand.TakeOut(1);
            offhand.MarkDirty();
            slot.Itemstack.Attributes.SetString(PackedAttr, tobacco.Variant["type"]);
            slot.Itemstack.Attributes.SetInt(PuffsAttr, puffsPerLoad);
            slot.Itemstack.Attributes.SetBool(LitAttr, false);
            slot.MarkDirty();
        }
        return true;
    }

    protected override void OnSmokedOut(ItemSlot slot, EntityAgent byEntity)
    {
        var attrs = slot.Itemstack!.Attributes;
        attrs.RemoveAttribute(PackedAttr);
        attrs.RemoveAttribute(PuffsAttr);
        attrs.RemoveAttribute(LitAttr);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        if (inSlot.Itemstack == null) return;

        string? variety = PackedWith(inSlot.Itemstack);
        dsc.AppendLine(variety == null
            ? Lang.Get("cigarettes:pipe-empty")
            : Lang.Get("cigarettes:pipe-packed", Lang.Get("cigarettes:variety-" + variety)));
    }
}
