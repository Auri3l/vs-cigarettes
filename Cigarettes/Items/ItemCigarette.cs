using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Cigarettes.Items;

/// <summary>
/// Cigarettes (hand-rolled and every brand) and cigars. Smoked down, one leaves a butt behind.
/// </summary>
public class ItemCigarette : ItemSmokable
{
    /// <summary>Stack attribute: the tobacco variety a hand-rolled cigarette was made from.</summary>
    public const string TobaccoAttr = "tobacco";

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

    public static void SetTobacco(ItemStack stack, string variety)
    {
        if (!string.IsNullOrEmpty(variety)) stack.Attributes.SetString(TobaccoAttr, variety);
    }

    /// <summary>Remember which tobacco went into a hand-rolled cigarette, so the tooltip can show it.</summary>
    public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe)
    {
        base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        ItemStack? tobacco = allInputslots.FirstOrDefault(s => s.Itemstack?.Collectible.FirstCodePart() == "shreddedtobacco")?.Itemstack;
        if (tobacco != null && outputSlot.Itemstack != null) SetTobacco(outputSlot.Itemstack, tobacco.Collectible.Variant["type"]);
    }

    /// <summary>Brands have a fixed blend (lang "blend-{brand}"); hand-rolled ones say what they were rolled from.</summary>
    public static string? BlendText(ItemStack stack)
    {
        string? variety = stack.Attributes.GetString(TobaccoAttr);
        string brand = stack.Collectible.Variant["brand"] ?? "";
        string? text = variety != null ? Lang.Get("cigarettes:variety-" + variety)
            : Lang.HasTranslation("cigarettes:blend-" + brand) ? Lang.Get("cigarettes:blend-" + brand) : null;
        if (text != null && variety != null && brand is "menthol" or "kretek")
        {
            text += " (" + Lang.Get(brand == "menthol" ? "cigarettes:blend-extra-menthol" : "cigarettes:blend-extra-cloves") + ")";
        }
        return text;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        string? blend = inSlot.Itemstack == null ? null : BlendText(inSlot.Itemstack);
        if (blend != null) dsc.AppendLine(Lang.Get("cigarettes:blend-label", blend));
    }

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
