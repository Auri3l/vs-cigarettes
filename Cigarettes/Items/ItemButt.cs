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
    const float FlickSeconds = 0.25f;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!firstEvent) return;

        // Let the ashtray and vanilla ground storage placement have their clicks
        if (byEntity.Controls.ShiftKey) return;
        if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockAshtray) return;

        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        return secondsUsed < FlickSeconds;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (secondsUsed < FlickSeconds - 0.05f || slot.Empty || byEntity.World.Side != EnumAppSide.Server) return;

        EntityProperties? type = byEntity.World.GetEntityType(new AssetLocation(Code.Domain, "flickedbutt"));
        if (type == null) return;

        var butt = (EntityFlickedButt)byEntity.World.ClassRegistry.CreateEntity(type);
        butt.FiredBy = byEntity;
        butt.ButtStack = slot.TakeOut(1);
        slot.MarkDirty();

        // A flick: off the fingertips, forward and a little up, much weaker than a thrown stone
        Vec3d from = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0);
        Vec3d ahead = from.AheadCopy(1, byEntity.Pos.Pitch, byEntity.Pos.Yaw);
        Vec3d velocity = (ahead - from) * 0.45 + new Vec3d(0, 0.08, 0);

        butt.Pos.SetPos(from);
        butt.Pos.Motion.Set(velocity);
        butt.World = byEntity.World;
        byEntity.World.SpawnEntity(butt);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
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
