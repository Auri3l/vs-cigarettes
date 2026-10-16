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

    public override void OnGameTick(float dt)
    {
        base.OnGameTick(dt);
        if (World.Side != EnumAppSide.Server || landed) return;

        bool timedOut = World.ElapsedMilliseconds - spawnedAtMs > MaxFlightMs;
        if (OnGround || Swimming || timedOut) Land();
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
