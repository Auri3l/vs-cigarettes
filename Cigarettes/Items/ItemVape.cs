using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Cigarettes.Items;

/// <summary>
/// A temporal vaporizer. Runs on e-liquid (a real liquid, poured from any liquid container
/// in the offhand) and on charge drawn from a gear in the offhand: a temporal gear fills it,
/// a rusty gear tops it up a little. No lighting, bigger clouds, one charge per puff.
/// </summary>
public class ItemVape : ItemSmokable
{
    const string FlavorAttr = "eliquidFlavor";
    const string ChargeAttr = "charge";

    int tankItems = 20;
    int maxCharge = 300;
    Dictionary<string, int> chargeFrom = new();

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        tankItems = Attributes?["tankItems"].AsInt(tankItems) ?? tankItems;
        maxCharge = Attributes?["maxCharge"].AsInt(maxCharge) ?? maxCharge;
        chargeFrom = Attributes?["chargeFrom"].AsObject<Dictionary<string, int>>() ?? chargeFrom;
    }

    protected override bool NeedsLighting => false;

    protected override int MaxPuffs(ItemStack stack) => tankItems;

    static int Liquid(ItemStack stack) => stack.Attributes.GetInt(PuffsAttr, 0);
    static int Charge(ItemStack stack) => stack.Attributes.GetInt(ChargeAttr, 0);
    static string? Flavor(ItemStack stack) => stack.Attributes.GetString(FlavorAttr);

    protected override bool HasSomethingToSmoke(ItemStack stack) => Liquid(stack) > 0;

    protected override string? WhyCantSmoke(ItemStack stack)
    {
        if (Liquid(stack) <= 0) return "vape-notank";
        if (Charge(stack) <= 0) return "vape-nocharge";
        return null;
    }

    protected override float Strength(ItemStack stack)
    {
        string? flavor = Flavor(stack);
        if (flavor == null) return baseStrength;
        Item? liquid = api.World.GetItem(new AssetLocation(Code.Domain, "eliquidportion-" + flavor));
        return liquid?.Attributes?["vapeStrength"].AsFloat(baseStrength) ?? baseStrength;
    }

    protected override bool TryAlternateInteract(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel)
    {
        if (slot.Itemstack == null) return false;

        // Buckets, jugs and barrels can't be held in the offhand: fill from one placed in the world
        if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockLiquidContainerBase placed)
        {
            BlockPos pos = blockSel.Position;
            if (TryFill(slot, byEntity, placed.GetContent(pos), n => placed.TryTakeContent(pos, n), () => { })) return true;
        }

        ItemSlot? offhand = byEntity.LeftHandItemSlot;
        ItemStack? other = offhand?.Itemstack;
        if (offhand == null || other == null) return false;

        if (chargeFrom.TryGetValue(other.Collectible.Code.ToString(), out int amount))
        {
            return TryCharge(slot, offhand, byEntity, amount);
        }

        if (other.Collectible.Code.Domain == Code.Domain && other.Collectible.FirstCodePart() == "eliquidcartridge")
        {
            return TryLoadCartridge(slot, offhand, byEntity);
        }

        if (other.Collectible is BlockLiquidContainerBase container)
        {
            return TryFill(slot, byEntity, container.GetContent(other), n => container.TryTakeContent(other, n), offhand.MarkDirty);
        }

        return false;
    }

    /// <summary>
    /// Clicks a cartridge in, like a pod: the tank becomes a full load of the cartridge's flavour,
    /// and whatever was left of the old one is thrown away with it.
    /// </summary>
    bool TryLoadCartridge(ItemSlot slot, ItemSlot offhand, EntityAgent byEntity)
    {
        ItemStack vape = slot.Itemstack!;
        ItemStack cartridge = offhand.Itemstack!;
        string flavor = cartridge.Collectible.Variant["flavor"];
        int puffs = Math.Min(tankItems, cartridge.Collectible.Attributes?["cartridgePuffs"].AsInt(tankItems) ?? tankItems);

        if (Liquid(vape) >= puffs && Flavor(vape) == flavor)
        {
            ShowError(byEntity, "vape-cartridgefull");
            return true;
        }

        if (byEntity.World.Side == EnumAppSide.Server)
        {
            offhand.TakeOut(1);
            offhand.MarkDirty();
            vape.Attributes.SetInt(PuffsAttr, puffs);
            vape.Attributes.SetString(FlavorAttr, flavor);
            slot.MarkDirty();
        }
        byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/player/build"), byEntity, (byEntity as EntityPlayer)?.Player, true, 8);
        return true;
    }

    bool IsCartridge(ItemStack? stack) => stack != null && stack.Collectible.Code.Domain == Code.Domain && stack.Collectible.FirstCodePart() == "eliquidcartridge";

    int ChargeFrom(ItemStack? stack) => stack != null && chargeFrom.TryGetValue(stack.Collectible.Code.ToString(), out int amount) ? amount : 0;

    /// <summary>
    /// In the inventory: drop a gear or a cartridge onto the vape (left or right click with it
    /// on the cursor) to charge it or click the cartridge in. Rusty gears can't go in the offhand,
    /// so this is the easy way.
    /// </summary>
    public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
    {
        if (priority == EnumMergePriority.DirectMerge && sinkStack.Collectible == this)
        {
            if (ChargeFrom(sourceStack) > 0 && Charge(sinkStack) < maxCharge) return 1;
            if (IsCartridge(sourceStack)) return 1;
        }
        return base.GetMergableQuantity(sinkStack, sourceStack, priority);
    }

    public override void TryMergeStacks(ItemStackMergeOperation op)
    {
        ItemStack? vape = op.SinkSlot.Itemstack;
        ItemStack? source = op.SourceSlot.Itemstack;
        if (op.CurrentPriority != EnumMergePriority.DirectMerge || vape == null || source == null || vape.Collectible != this)
        {
            base.TryMergeStacks(op);
            return;
        }

        int amount = ChargeFrom(source);
        if (amount > 0 && Charge(vape) < maxCharge)
        {
            vape.Attributes.SetInt(ChargeAttr, Math.Min(maxCharge, Charge(vape) + amount));
            if (source.Collectible.Code.Path == "gear-temporal" && op.ActingPlayer?.Entity is EntityAgent agent && op.World.Side == EnumAppSide.Server)
            {
                op.World.Api.ModLoader.GetModSystem<Smoking.AchievementSystem>()?.OnTemporalCharge(agent);
            }
        }
        else if (IsCartridge(source))
        {
            vape.Attributes.SetInt(PuffsAttr, Math.Min(tankItems, source.Collectible.Attributes?["cartridgePuffs"].AsInt(tankItems) ?? tankItems));
            vape.Attributes.SetString(FlavorAttr, source.Collectible.Variant["flavor"]);
        }
        else
        {
            base.TryMergeStacks(op);
            return;
        }

        op.SourceSlot.TakeOut(1);
        op.MovedQuantity = 1;
        op.SourceSlot.MarkDirty();
        op.SinkSlot.MarkDirty();
    }

    bool TryCharge(ItemSlot slot, ItemSlot offhand, EntityAgent byEntity, int amount)
    {
        int charge = Charge(slot.Itemstack!);
        if (charge >= maxCharge)
        {
            ShowError(byEntity, "vape-fullcharge");
            return true;
        }

        if (byEntity.World.Side == EnumAppSide.Server)
        {
            string gear = offhand.Itemstack?.Collectible.Code.Path ?? "";
            offhand.TakeOut(1);
            offhand.MarkDirty();
            slot.Itemstack!.Attributes.SetInt(ChargeAttr, Math.Min(maxCharge, charge + amount));
            slot.MarkDirty();
            if (gear == "gear-temporal")
            {
                byEntity.Api.ModLoader.GetModSystem<Smoking.AchievementSystem>()?.OnTemporalCharge(byEntity);
            }
        }
        return true;
    }

    bool TryFill(ItemSlot slot, EntityAgent byEntity, ItemStack? content, System.Func<int, ItemStack?> takeContent, System.Action markSourceDirty)
    {
        CollectibleObject? liquid = content?.Collectible;
        if (liquid == null || liquid.Code.Domain != Code.Domain || liquid.FirstCodePart() != "eliquidportion") return false;

        ItemStack vape = slot.Itemstack!;
        string flavor = liquid.Variant["flavor"];
        int have = Liquid(vape);

        if (have > 0 && Flavor(vape) != flavor)
        {
            ShowError(byEntity, "vape-mixedflavor");
            return true;
        }

        int want = tankItems - have;
        if (want <= 0)
        {
            ShowError(byEntity, "vape-tankfull");
            return true;
        }

        if (byEntity.World.Side == EnumAppSide.Server)
        {
            ItemStack? taken = takeContent(want);
            if (taken != null && taken.StackSize > 0)
            {
                vape.Attributes.SetInt(PuffsAttr, have + taken.StackSize);
                vape.Attributes.SetString(FlavorAttr, flavor);
                markSourceDirty();
                slot.MarkDirty();
            }
        }
        return true;
    }

    protected override void OnPuffTaken(ItemSlot slot, EntityAgent byEntity)
    {
        ItemStack vape = slot.Itemstack!;
        vape.Attributes.SetInt(ChargeAttr, Math.Max(0, Charge(vape) - 1));
    }

    protected override void OnSmokedOut(ItemSlot slot, EntityAgent byEntity)
    {
        // Tank ran dry: the vape stays, ready for a refill of any flavour
        var attrs = slot.Itemstack!.Attributes;
        attrs.RemoveAttribute(PuffsAttr);
        attrs.RemoveAttribute(FlavorAttr);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        ItemStack? vape = inSlot.Itemstack;
        if (vape == null) return;

        dsc.AppendLine(Lang.Get("cigarettes:vape-charge", Charge(vape), maxCharge));
        string? flavor = Flavor(vape);
        dsc.AppendLine(flavor == null
            ? Lang.Get("cigarettes:vape-tank-empty")
            : Lang.Get("cigarettes:vape-tank", Liquid(vape), tankItems, Lang.Get("cigarettes:flavor-" + flavor)));
    }
}
