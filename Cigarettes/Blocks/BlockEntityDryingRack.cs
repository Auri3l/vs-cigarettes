using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Cigarettes.Blocks;

public class BlockEntityDryingRack : BlockEntityDisplay
{
    // The inventory must exist before the block is known (saved contents load first),
    // so it's always this big; the block's "slots" attribute says how many are usable
    // (4 on the drying rack, 12 on the drying frame), hung in "rows".
    const int MaxSlots = 12;

    readonly InventoryGeneric inv;
    float dryingSpeedMul = 6f;
    int slots = 4;
    int rows = 1;

    public override InventoryBase Inventory => inv;
    public override string InventoryClassName => "cigarettes-dryingrack";
    public override string AttributeTransformCode => "onDryingRackTransform";

    public BlockEntityDryingRack()
    {
        inv = new InventoryGeneric(MaxSlots, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        dryingSpeedMul = Block?.Attributes?["dryingSpeedMul"].AsFloat(dryingSpeedMul) ?? dryingSpeedMul;
        slots = GameMath.Clamp(Block?.Attributes?["slots"].AsInt(slots) ?? slots, 1, MaxSlots);
        rows = GameMath.Clamp(Block?.Attributes?["rows"].AsInt(rows) ?? rows, 1, slots);
        base.Initialize(api);
    }

    // Drying goes faster on the rack; every other transition (perish etc.) runs at the normal rate
    protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
    {
        float mul = base.Inventory_OnAcquireTransitionSpeed(transType, stack, baseMul);
        if (transType == EnumTransitionType.Dry) mul *= dryingSpeedMul;
        return mul;
    }

    static bool IsDryable(ItemStack? stack, IWorldAccessor world)
    {
        return stack?.Collectible.GetTransitionableProperties(world, stack, null)?
            .Any(p => p.Type == EnumTransitionType.Dry) == true;
    }

    internal bool OnInteract(IPlayer byPlayer)
    {
        ItemSlot hand = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (hand.Empty)
        {
            // Take down the most recently hung item
            ItemSlot? full = inv.Take(slots).LastOrDefault(s => !s.Empty);
            if (full == null) return false;
            ItemStack taken = full.TakeOut(1);
            if (!byPlayer.InventoryManager.TryGiveItemstack(taken, true))
            {
                Api.World.SpawnItemEntity(taken, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            full.MarkDirty();
            MarkDirty(true);
            return true;
        }

        if (!IsDryable(hand.Itemstack, Api.World)) return false;

        ItemSlot? free = inv.Take(slots).FirstOrDefault(s => s.Empty);
        if (free == null) return false;

        if (hand.TryPutInto(Api.World, free, 1) > 0)
        {
            hand.MarkDirty();
            MarkDirty(true);
            return true;
        }

        return false;
    }

    // Hang items side by side along each bar, rows stacked from the top bar down
    protected override float[][] genTransformationMatrices()
    {
        float[][] tfMatrices = new float[MaxSlots][];
        int perRow = (slots + rows - 1) / rows;
        // One bar: big leaves hanging from it. Several bars: smaller leaves, one row under each bar.
        float scale = rows == 1 ? 0.5f : 0.3f;
        for (int i = 0; i < MaxSlots; i++)
        {
            int row = i / perRow, col = i % perRow;
            float x = (col + 1f) / (perRow + 1f);
            float y = rows == 1 ? 0.35f : 0.62f - row * 0.3f;
            tfMatrices[i] = new Matrixf()
                .Translate(0.5f, 0, 0.5f)
                .RotateYDeg(Block.Shape.rotateY)
                .Translate(x - 0.5f, y, -0.03f)
                .RotateXDeg(90)
                .Scale(scale, scale, scale)
                .Translate(-0.5f, 0, -0.5f)
                .Values;
        }
        return tfMatrices;
    }
}
