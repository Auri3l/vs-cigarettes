using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Cigarettes.Blocks;

/// <summary>
/// The block side of the batch furniture: forwards clicks to the block entity
/// and runs the "hold right click to work" loop with a few particles.
/// </summary>
public class BlockWorkstation : Block
{
    SimpleParticleProperties? workParticles;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        workParticles = new SimpleParticleProperties(
            1, 2, ColorUtil.ToRgba(255, 150, 105, 60),
            new Vec3d(), new Vec3d(0.6, 0.1, 0.6),
            new Vec3f(-0.3f, 0.3f, -0.3f), new Vec3f(0.6f, 0.6f, 0.6f),
            0.6f, 1f, 0.1f, 0.3f, EnumParticleModel.Cube);
    }

    static BlockEntityWorkstation? Station(IWorldAccessor world, BlockSelection sel) =>
        world.BlockAccessor.GetBlockEntity(sel.Position) as BlockEntityWorkstation;

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityWorkstation? station = Station(world, blockSel);
        if (station == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        if (station.TryLoadOrUnload(byPlayer)) return true;
        return station.CanWork;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityWorkstation? station = Station(world, blockSel);
        if (station == null || !station.CanWork) return false;

        if (world.Side == EnumAppSide.Client && workParticles != null && world.Rand.NextDouble() < 0.3)
        {
            workParticles.MinPos = blockSel.Position.ToVec3d().Add(0.2, 0.9, 0.2);
            world.SpawnParticles(workParticles);
        }

        return secondsUsed < station.WorkSeconds;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityWorkstation? station = Station(world, blockSel);
        if (station == null || secondsUsed < station.WorkSeconds - 0.05f) return;
        station.FinishWork(byPlayer);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return new[]
        {
            new WorldInteraction { ActionLangCode = "cigarettes:blockhelp-load", MouseButton = EnumMouseButton.Right },
            new WorldInteraction { ActionLangCode = "cigarettes:blockhelp-work", MouseButton = EnumMouseButton.Right },
            new WorldInteraction { ActionLangCode = "cigarettes:blockhelp-unload", MouseButton = EnumMouseButton.Right, HotKeyCode = "shift" },
        };
    }
}
