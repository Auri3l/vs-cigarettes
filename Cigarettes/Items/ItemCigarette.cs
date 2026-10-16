using Vintagestory.API.Common;

namespace Cigarettes.Items;

/// <summary>
/// Cigarettes (hand-rolled and every brand) and cigars. Smoked down, one leaves a butt behind.
/// </summary>
public class ItemCigarette : ItemSmokable
{
    int maxPuffs = 8;
    AssetLocation? buttCode;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        maxPuffs = Attributes?["maxPuffs"].AsInt(maxPuffs) ?? maxPuffs;
        string? butt = Attributes?["buttCode"].AsString();
        if (butt != null) buttCode = AssetLocation.Create(butt, Code.Domain);
    }

    protected override int MaxPuffs(ItemStack stack) => maxPuffs;

    protected override void OnSmokedOut(ItemSlot slot, EntityAgent byEntity)
    {
        slot.TakeOut(1);
        if (buttCode == null || byEntity.World.GetItem(buttCode) is not Item buttItem) return;

        var butt = new ItemStack(buttItem);
        if (!byEntity.TryGiveItemStack(butt))
        {
            byEntity.World.SpawnItemEntity(butt, byEntity.Pos.XYZ);
        }
    }
}
