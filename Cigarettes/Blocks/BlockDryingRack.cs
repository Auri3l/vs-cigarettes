using Vintagestory.API.Common;

namespace Cigarettes.Blocks;

/// <summary>
/// A wooden rack that hangs up to four dryable items (tobacco leaves first of all)
/// and dries them much faster than a chest or the ground would.
/// </summary>
public class BlockDryingRack : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityDryingRack rack)
        {
            return rack.OnInteract(byPlayer);
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
