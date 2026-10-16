using System.IO;
using System.Linq;
using Cigarettes.Blocks;
using Cigarettes.Smoking;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Cigarettes.Entities;

/// <summary>
/// A flicked butt in flight. When it comes to rest it turns into ground storage
/// (a pile of up to 64 butts per block, joining a pile already there if there's room), lands in an ashtray
/// if it hits one, and otherwise just drops as an item (in water, on a wall, no room).
/// </summary>
public class EntityFlickedButt : Entity
{
    const long MaxFlightMs = 15000;

    public EntityAgent? FiredBy;
    public ItemStack? ButtStack;

    long spawnedAtMs;
    bool landed;

    public override bool IsInteractable => false;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        spawnedAtMs = World.ElapsedMilliseconds;
    }

    // Flight path: a sideways and vertical wobble at a 2:1 frequency ratio traces a figure eight around the arc
    const double LoopsPerSecond = 1.5;
    const double SideAmplitude = 0.3;  // blocks
    const double UpAmplitude = 0.15;   // blocks
    const float SpinPerSecond = 14f;   // radians, end over end

    Vec3d? side;
    double flightSeconds;

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);

        if (!OnGround && !Swimming && !landed)
        {
            // Tumble end over end while airborne (both sides, so the client renders it)
            Pos.Pitch = (Pos.Pitch + SpinPerSecond * dt) % GameMath.TWOPI;
            Pos.Roll = (Pos.Roll + SpinPerSecond * 0.6f * dt) % GameMath.TWOPI;
            if (World.Side == EnumAppSide.Server) FigureEight(dt);
        }

        if (World.Side != EnumAppSide.Server || landed) return;

        bool timedOut = World.ElapsedMilliseconds - spawnedAtMs > MaxFlightMs;
        if (OnGround || Swimming || timedOut) Land();
    }

    /// <summary>
    /// Offsets the flight by (sin 2wt sideways, sin wt up). Motion is in blocks per 1/60 s,
    /// so each tick adds the change in the offset's velocity on top of the ballistic arc.
    /// </summary>
    void FigureEight(float dt)
    {
        double w = GameMath.TWOPI * LoopsPerSecond;
        double sideVel(double t) => SideAmplitude * 2 * w * System.Math.Cos(2 * w * t);
        double upVel(double t) => UpAmplitude * w * System.Math.Cos(w * t);

        if (side == null)
        {
            Vec3d flat = new Vec3d(Pos.Motion.X, 0, Pos.Motion.Z);
            if (flat.Length() < 1e-4) return;
            flat.Normalize();
            side = new Vec3d(-flat.Z, 0, flat.X);
            // Start the wobble's own velocity, so the offset swings around the arc instead of drifting off it
            Pos.Motion.Add(side.X * sideVel(0) / 60.0, upVel(0) / 60.0, side.Z * sideVel(0) / 60.0);
        }

        double t0 = flightSeconds, t1 = flightSeconds + dt;
        flightSeconds = t1;

        double dSide = (sideVel(t1) - sideVel(t0)) / 60.0;
        double dUp = (upVel(t1) - upVel(t0)) / 60.0;
        Pos.Motion.Add(side.X * dSide, dUp, side.Z * dSide);
    }

    void Land()
    {
        landed = true;
        if (ButtStack != null)
        {
            string where = Swimming ? "lost" : TryAshtray() ? "ashtray" : TryGroundStorage() ? "ground" : "lost";
            if (where == "lost") World.SpawnItemEntity(ButtStack, Pos.XYZ);
            if (FiredBy != null) World.Api.ModLoader.GetModSystem<AchievementSystem>()?.OnFlick(FiredBy, where);
        }
        Die(EnumDespawnReason.Removed);
    }

    bool TryAshtray()
    {
        BlockPos pos = Pos.AsBlockPos;
        foreach (BlockPos p in new[] { pos, pos.DownCopy() })
        {
            if (World.BlockAccessor.GetBlockEntity(p) is BlockEntityAshtray tray
                && tray.TryAddButt(ButtStack!.Collectible.Code.ToString(), FiredBy))
            {
                return true;
            }
        }
        return false;
    }

    bool TryGroundStorage()
    {
        IBlockAccessor ba = World.BlockAccessor;
        Block groundStorage = World.GetBlock(new AssetLocation("game:groundstorage"));
        GroundStorageProperties? props = ButtStack!.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;
        if (groundStorage == null || props == null) return false;

        BlockPos pos = Pos.AsBlockPos;

        // Join an existing pile of the same butts
        if (ba.GetBlockEntity(pos) is BlockEntityGroundStorage existing) return PutInto(existing, props);

        Block here = ba.GetBlock(pos);
        BlockPos below = pos.DownCopy();
        if (!here.IsReplacableBy(groundStorage) || !ba.GetBlock(below).CanAttachBlockAt(ba, groundStorage, below, BlockFacing.UP)) return false;

        ba.SetBlock(groundStorage.Id, pos);
        if (ba.GetBlockEntity(pos) is BlockEntityGroundStorage created && PutInto(created, props)) return true;

        ba.SetBlock(0, pos);
        return false;
    }

    bool PutInto(BlockEntityGroundStorage storage, GroundStorageProperties props)
    {
        if (storage.StorageProps == null) storage.ForceStorageProps(props);
        else if (storage.StorageProps.Layout != props.Layout) return false;

        // A "Stacking" pile keeps everything in its first slot, up to stackingCapacity (64 butts)
        ItemSlot slot = storage.Inventory[0];
        if (slot.Empty)
        {
            slot.Itemstack = ButtStack!.Clone();
        }
        else if (slot.Itemstack.Equals(World, ButtStack!, Vintagestory.API.Config.GlobalConstants.IgnoredStackAttributes)
                 && slot.StackSize < props.StackingCapacity)
        {
            slot.Itemstack.StackSize++;
        }
        else
        {
            return false;
        }

        slot.MarkDirty();
        storage.MarkDirty(true);
        return true;
    }

    // Keep the butt through a save/load mid-flight
    public override void ToBytes(BinaryWriter writer, bool forClient)
    {
        base.ToBytes(writer, forClient);
        writer.Write(ButtStack != null);
        ButtStack?.ToBytes(writer);
    }

    public override void FromBytes(BinaryReader reader, bool fromServer)
    {
        base.FromBytes(reader, fromServer);
        if (reader.ReadBoolean())
        {
            ButtStack = new ItemStack(reader);
        }
    }

    public override void OnEntityLoaded()
    {
        base.OnEntityLoaded();
        ButtStack?.ResolveBlockOrItem(World);
    }
}
