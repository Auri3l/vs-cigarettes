using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Cigarettes.Items;

/// <summary>
/// Matches and lighters. Held in the offhand they light smokables (see ItemSmokable).
/// Held in the main hand, right click lights anything a firestarter can (firepits,
/// forges, torches, charcoal pits...) on the spot, for one use of durability.
/// </summary>
public class ItemIgniter : Item
{
    // Firestarters need a few seconds of drilling; we pretend that already happened
    const float SecondsIgniting = 10f;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!firstEvent || blockSel == null || slot.Itemstack == null) return;

        // Lighting comes first, with or without sneaking (sneaking is how you get past a firepit's own GUI)
        if (TryIgnite(slot, byEntity, blockSel))
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        // Otherwise sneak + right click places it as ground storage (CollectibleBehaviorGroundStorable)
        if (byEntity.Controls.ShiftKey)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }

    bool TryIgnite(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel)
    {
        IWorldAccessor world = byEntity.World;
        BlockPos pos = blockSel.Position;
        IIgnitable? ignitable = world.BlockAccessor.GetBlock(pos).GetInterface<IIgnitable>(world, pos);
        if (ignitable == null) return false;

        IPlayer? player = (byEntity as EntityPlayer)?.Player;
        if (player != null && !world.Claims.TryAccess(player, pos, EnumBlockAccessFlags.Use)) return false;

        EnumIgniteState state = ignitable.OnTryIgniteBlock(byEntity, pos, SecondsIgniting);
        if (state == EnumIgniteState.NotIgnitablePreventDefault) return true;
        if (state != EnumIgniteState.Ignitable && state != EnumIgniteState.IgniteNow) return false;

        world.PlaySoundAt(new AssetLocation("game:sounds/torch-ignite"), pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, player, false, 16);

        if (world.Side == EnumAppSide.Server)
        {
            EnumHandling ignited = EnumHandling.PassThrough;
            ignitable.OnTryIgniteBlockOver(byEntity, pos, SecondsIgniting, ref ignited);
            DamageItem(world, byEntity, slot, 1);
        }
        return true;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction
            {
                ActionLangCode = "cigarettes:heldhelp-ignite",
                MouseButton = EnumMouseButton.Right,
            }
        }.Append(base.GetHeldInteractionHelp(inSlot));
    }
}
