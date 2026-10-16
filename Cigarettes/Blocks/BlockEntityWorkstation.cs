using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Cigarettes.Blocks;

/// <summary>
/// Shared base of the batch furniture (tobacco cutter, rolling table).
/// Right click with an ingredient loads it; hold right click with an empty hand to work
/// the whole batch at once; sneak + right click with an empty hand takes the ingredients back.
/// Finished goods go straight to the player.
/// </summary>
public abstract class BlockEntityWorkstation : BlockEntityContainer
{
    protected InventoryGeneric inv;

    public override InventoryBase Inventory => inv;

    /// <summary>How long the empty hand has to be held to work one batch.</summary>
    public float WorkSeconds { get; protected set; } = 3f;

    protected BlockEntityWorkstation(int slotCount)
    {
        inv = new InventoryGeneric(slotCount, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        WorkSeconds = Block?.Attributes?["workSeconds"].AsFloat(WorkSeconds) ?? WorkSeconds;
        base.Initialize(api);
    }

    /// <summary>Which slot this stack goes in, or null if the station doesn't take it.</summary>
    protected abstract ItemSlot? SlotFor(ItemStack stack);

    /// <summary>Counter name for the achievements ("cut", "rolled").</summary>
    protected abstract string BatchKind { get; }

    /// <summary>Whether there's enough loaded to work a batch.</summary>
    public abstract bool CanWork { get; }

    /// <summary>Does the batch (server side) and returns what was made, or null.</summary>
    protected abstract ItemStack[]? DoWork(IPlayer byPlayer);

    /// <summary>Returns true if the click was a load/unload, false if it should start working.</summary>
    internal bool TryLoadOrUnload(IPlayer byPlayer)
    {
        ItemSlot hand = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (hand.Empty)
        {
            if (!byPlayer.Entity.Controls.ShiftKey) return false;

            // Sneak + empty hand: take everything back out
            foreach (ItemSlot slot in inv)
            {
                if (slot.Empty) continue;
                if (Api.Side == EnumAppSide.Server) Give(byPlayer, slot.TakeOutWhole());
            }
            MarkDirty(true);
            return true;
        }

        ItemSlot? target = SlotFor(hand.Itemstack);
        if (target == null) return true;

        if (Api.Side == EnumAppSide.Server)
        {
            int moved = hand.TryPutInto(Api.World, target, hand.StackSize);
            if (moved > 0)
            {
                hand.MarkDirty();
                MarkDirty(true);
            }
        }
        return true;
    }

    internal void FinishWork(IPlayer byPlayer)
    {
        if (Api.Side != EnumAppSide.Server || !CanWork) return;

        ItemStack[]? made = DoWork(byPlayer);
        if (made == null) return;

        foreach (ItemStack stack in made) Give(byPlayer, stack);
        MarkDirty(true);

        Api.ModLoader.GetModSystem<Smoking.AchievementSystem>()?.OnBatch(byPlayer.Entity, BatchKind);
    }

    protected void Give(IPlayer byPlayer, ItemStack stack)
    {
        if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 1.1, 0.5));
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        foreach (ItemSlot slot in inv)
        {
            if (!slot.Empty) dsc.AppendLine($"{slot.StackSize}× {slot.Itemstack.GetName()}");
        }
        if (inv.Empty) dsc.AppendLine(Lang.Get("cigarettes:workstation-empty"));
    }
}

/// <summary>
/// A guillotine-style cutter: load up to 32 dried leaves of one variety,
/// crank it once, get two shredded tobacco per leaf.
/// </summary>
public class BlockEntityTobaccoCutter : BlockEntityWorkstation
{
    public BlockEntityTobaccoCutter() : base(1) { }

    public override string InventoryClassName => "cigarettes-tobaccocutter";
    protected override string BatchKind => "cut";

    protected override ItemSlot? SlotFor(ItemStack stack)
    {
        CollectibleObject leaf = stack.Collectible;
        bool driedLeaf = leaf.Code.Domain == CigarettesModSystem.ModId && leaf.FirstCodePart() == "tobaccoleaf" && leaf.Variant["state"] == "dried";
        if (!driedLeaf) return null;
        // One variety at a time
        if (!inv[0].Empty && !inv[0].Itemstack.Equals(Api.World, stack, GlobalConstants.IgnoredStackAttributes)) return null;
        return inv[0];
    }

    public override bool CanWork => !inv[0].Empty;

    protected override ItemStack[]? DoWork(IPlayer byPlayer)
    {
        ItemStack leaves = inv[0].TakeOutWhole();
        Item? shredded = Api.World.GetItem(new AssetLocation(CigarettesModSystem.ModId, "shreddedtobacco-" + leaves.Collectible.Variant["type"]));
        if (shredded == null) return null;

        // Shredded tobacco stacks to 64, so a full load of 32 leaves fits in one stack
        return new[] { new ItemStack(shredded, leaves.StackSize * 2) };
    }
}

/// <summary>
/// A rolling table: load shredded tobacco and rolling papers, optionally menthol crystals or
/// dried cloves. Each roll turns up to 8 tobacco + 8 papers into 24 cigarettes per pair,
/// more than the 20 you get rolling by hand. Menthol makes menthols, cloves make kreteks.
/// </summary>
public class BlockEntityRollingTable : BlockEntityWorkstation
{
    const int TobaccoSlot = 0, PaperSlot = 1, AdditiveSlot = 2;
    const int BatchSize = 8;
    const int CigarettesPerPair = 24;

    public BlockEntityRollingTable() : base(3) { }

    public override string InventoryClassName => "cigarettes-rollingtable";
    protected override string BatchKind => "rolled";

    protected override ItemSlot? SlotFor(ItemStack stack)
    {
        CollectibleObject c = stack.Collectible;
        if (c.Code.Domain != CigarettesModSystem.ModId) return null;

        string first = c.FirstCodePart();
        ItemSlot? slot = first switch
        {
            "shreddedtobacco" => inv[TobaccoSlot],
            "rollingpaper" => inv[PaperSlot],
            "mentholcrystals" => inv[AdditiveSlot],
            "herb" when c.Code.Path == "herb-clove-dried" => inv[AdditiveSlot],
            _ => null,
        };
        if (slot == null) return null;
        if (!slot.Empty && !slot.Itemstack.Equals(Api.World, stack, GlobalConstants.IgnoredStackAttributes)) return null;
        return slot;
    }

    public override bool CanWork => !inv[TobaccoSlot].Empty && !inv[PaperSlot].Empty;

    string Brand()
    {
        ItemStack? additive = inv[AdditiveSlot].Itemstack;
        if (additive == null) return "handrolled";
        return additive.Collectible.FirstCodePart() == "mentholcrystals" ? "menthol" : "kretek";
    }

    protected override ItemStack[]? DoWork(IPlayer byPlayer)
    {
        int pairs = GameMath.Min(BatchSize, inv[TobaccoSlot].StackSize, inv[PaperSlot].StackSize);
        if (!inv[AdditiveSlot].Empty) pairs = GameMath.Min(pairs, inv[AdditiveSlot].StackSize);

        Item? cigarette = Api.World.GetItem(new AssetLocation(CigarettesModSystem.ModId, "cigarette-" + Brand()));
        if (cigarette == null || pairs <= 0) return null;

        string variety = inv[TobaccoSlot].Itemstack.Collectible.Variant["type"] ?? "";
        inv[TobaccoSlot].TakeOut(pairs);
        inv[PaperSlot].TakeOut(pairs);
        if (!inv[AdditiveSlot].Empty) inv[AdditiveSlot].TakeOut(pairs);

        // Cigarettes stack to 20, so hand them over in stacks
        int total = pairs * CigarettesPerPair;
        var made = new System.Collections.Generic.List<ItemStack>();
        while (total > 0)
        {
            int n = GameMath.Min(total, cigarette.MaxStackSize);
            var stack = new ItemStack(cigarette, n);
            Items.ItemCigarette.SetTobacco(stack, variety);
            made.Add(stack);
            total -= n;
        }
        return made.ToArray();
    }
}
