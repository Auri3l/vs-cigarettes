using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Cigarettes.Blocks;

public class BlockEntityAshtray : BlockEntity
{
    const int Capacity = 12;

    // Butts by item code, so cigar and cigarette butts both come back out as themselves
    readonly System.Collections.Generic.List<string> butts = new();

    public bool IsFull => butts.Count >= Capacity;

    /// <summary>Adds one butt by item code (server side). Used by hand deposits and flicked butts.</summary>
    public bool TryAddButt(string code, EntityAgent? byEntity)
    {
        if (IsFull) return false;
        butts.Add(code);
        Changed();
        if (IsFull && byEntity != null)
        {
            Api.ModLoader.GetModSystem<Smoking.AchievementSystem>()?.OnAshtrayFull(byEntity);
        }
        return true;
    }

    internal static bool IsButt(ItemStack? stack)
    {
        return stack != null && stack.Collectible.Code.Domain == CigarettesModSystem.ModId
            && stack.Collectible.Code.Path.EndsWith("butt");
    }

    internal bool OnInteract(IPlayer byPlayer)
    {
        ItemSlot hand = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (IsButt(hand.Itemstack))
        {
            if (IsFull) return false;
            if (Api.Side == EnumAppSide.Server && TryAddButt(hand.Itemstack!.Collectible.Code.ToString(), byPlayer.Entity))
            {
                hand.TakeOut(1);
                hand.MarkDirty();
            }
            return true;
        }

        if (hand.Empty && butts.Count > 0)
        {
            if (Api.Side == EnumAppSide.Server)
            {
                Item? item = Api.World.GetItem(new AssetLocation(butts[^1]));
                butts.RemoveAt(butts.Count - 1);
                if (item != null)
                {
                    var stack = new ItemStack(item);
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }
                Changed();
            }
            return true;
        }

        return false;
    }

    void Changed()
    {
        string fill = butts.Count == 0 ? "empty" : butts.Count < Capacity ? "some" : "full";
        Block next = Api.World.GetBlock(Block.CodeWithVariant("fill", fill));
        if (next != null && next.Id != Block.Id)
        {
            Api.World.BlockAccessor.ExchangeBlock(next.Id, Pos);
            Block = next;
        }
        MarkDirty(true);
    }

    public override void OnBlockBroken(IPlayer? byPlayer = null)
    {
        // Knocking it over spills the butts
        foreach (string code in butts)
        {
            Item? item = Api.World.GetItem(new AssetLocation(code));
            if (item != null) Api.World.SpawnItemEntity(new ItemStack(item), Pos.ToVec3d().Add(0.5, 0.3, 0.5));
        }
        butts.Clear();
        base.OnBlockBroken(byPlayer);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("butts", string.Join(",", butts));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        butts.Clear();
        string saved = tree.GetString("butts", "");
        if (saved.Length > 0) butts.AddRange(saved.Split(','));
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        dsc.AppendLine(Lang.Get("cigarettes:ashtray-butts", butts.Count, Capacity));
    }
}
