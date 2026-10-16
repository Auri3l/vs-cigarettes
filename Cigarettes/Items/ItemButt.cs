using Cigarettes.Blocks;
using Cigarettes.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Cigarettes.Items;

/// <summary>
/// A cigarette or cigar butt. Right click to flick it: it flies off and, where it lands,
/// becomes ground storage (or drops into an ashtray, if you're that good).
/// Sneak + right click still places it as ground storage the vanilla way,
/// and right clicking an ashtray still stubs it out there.
/// </summary>
public class ItemButt : Item
{
    // Hold to aim (with the vanilla throwing reticle and accuracy), release to flick
    const float WindupSeconds = 0.35f;
    const string AimAnimation = "throwaim";
    const string ThrowAnimation = "throw";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!firstEvent) return;

        // Sneak + right click on a block places it as ground storage (CollectibleBehaviorGroundStorable)
        if (byEntity.Controls.ShiftKey && blockSel != null)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }
        // Let the ashtray have its click
        if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockAshtray) return;

        byEntity.Attributes.SetInt("aiming", 1);
        byEntity.Attributes.SetInt("aimingCancel", 0);
        byEntity.StartAnimation(AimAnimation);
        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        // Keep aiming until released
        return true;
    }

    static void StopAiming(EntityAgent byEntity)
    {
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation(AimAnimation);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        bool cancelled = byEntity.Attributes.GetInt("aimingCancel") == 1;
        StopAiming(byEntity);
        if (cancelled || secondsUsed < WindupSeconds || slot.Empty) return;

        byEntity.StartAnimation(ThrowAnimation);
        if (byEntity.World.Side != EnumAppSide.Server) return;

        EntityProperties? type = byEntity.World.GetEntityType(new AssetLocation(Code.Domain, "flickedbutt"));
        if (type == null) return;

        var butt = (EntityFlickedButt)byEntity.World.ClassRegistry.CreateEntity(type);
        butt.FiredBy = byEntity;
        butt.ButtStack = slot.TakeOut(1);
        slot.MarkDirty();

        // Spread shrinks the longer you aim, like a thrown stone (the reticle shows it)
        double accuracy = byEntity.Attributes.GetDouble("aimingAccuracy", 0.5);
        double spread = (1 - accuracy) * 0.2;
        var rand = byEntity.World.Rand;
        float pitch = byEntity.Pos.Pitch + (float)((rand.NextDouble() - 0.5) * spread);
        float yaw = byEntity.Pos.Yaw + (float)((rand.NextDouble() - 0.5) * spread);

        // A flick: off the fingertips, forward and a little up, much weaker than a thrown stone
        Vec3d from = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0);
        Vec3d ahead = from.AheadCopy(1, pitch, yaw);
        Vec3d velocity = (ahead - from) * 0.5 + new Vec3d(0, 0.06, 0);

        butt.Pos.SetPos(from);
        butt.Pos.Motion.Set(velocity);
        butt.World = byEntity.World;
        byEntity.World.SpawnEntity(butt);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        byEntity.Attributes.SetInt("aimingCancel", 1);
        StopAiming(byEntity);
        return true;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction { ActionLangCode = "cigarettes:heldhelp-flick", MouseButton = EnumMouseButton.Right },
        }.Append(base.GetHeldInteractionHelp(inSlot));
    }
}
