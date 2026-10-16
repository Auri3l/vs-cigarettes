using System.Linq;
using Vintagestory.API.Client;
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
        inv.OnAcquireTransitionSpeed += OnAcquireTransitionSpeed;
    }

    // Drying goes faster on the rack; every other transition (perish etc.) runs at the normal rate.
    // The inventory multiplies together the factors from every subscribed handler (the container's own included).
    float OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float mulByConfig)
    {
        return transType == EnumTransitionType.Dry ? dryingSpeedMul : 1f;
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

    // Hang items upright from the pegs under each bar, rows stacked from the top bar down.
    // Peg positions match shapes/block/dryingrack.json and dryingframe.json: four pegs per bar
    // from x 4.4/16 to 11.6/16, hanging below y 12.4/16 (rack) or 14.4, 9.6 and 4.8/16 (frame).
    protected override float[][] genTransformationMatrices()
    {
        float[][] tfMatrices = new float[MaxSlots][];
        int perRow = (slots + rows - 1) / rows;
        // One bar: big leaves. Several bars: smaller leaves that fit between the bars.
        // Leaves and herbs use their 3D "displayedShape" (shapes/item/*-hanging.json), turned so they don't cover each other.
        float scale = rows == 1 ? 0.78f : 0.32f;
        for (int i = 0; i < MaxSlots; i++)
        {
            int row = i / perRow, col = i % perRow;
            float x = perRow == 1 ? 0.5f : 0.275f + col * 0.45f / (perRow - 1);
            float pegY = rows == 1 ? 0.775f : 0.9f - row * 0.3f;
            // Neighbours overlap, so alternate them in front of and behind the bar to avoid z-fighting
            float z = col % 2 == 0 ? -0.05f : 0.05f;
            tfMatrices[i] = new Matrixf()
                .Translate(0.5f, 0, 0.5f)
                .RotateYDeg(Block.Shape.rotateY)
                .Translate(x - 0.5f, pegY - scale, z)
                .Scale(scale, scale, scale)
                .Translate(-0.5f, 0, -0.5f)
                .Values;
        }
        return tfMatrices;
    }
}
