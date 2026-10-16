using Vintagestory.API.Common;

namespace Cigarettes.Blocks;

/// <summary>
/// Stub out a butt by right clicking the ashtray with it. Empty hand takes the butts back out.
/// </summary>
public class BlockAshtray : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityAshtray ashtray)
        {
            return ashtray.OnInteract(byPlayer);
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
