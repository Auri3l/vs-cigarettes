using System.Collections.Generic;
using System.Text;
using Cigarettes.Smoking;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Cigarettes.Items;

/// <summary>
/// Shared behaviour of everything you can smoke: lighting it from the offhand,
/// holding right click to take a puff, smoke particles, and the nicotine effect.
/// Subclasses decide how many puffs there are and what happens when it's smoked out.
/// </summary>
public abstract class ItemSmokable : Item
{
    protected const string LitAttr = "lit";
    protected const string PuffsAttr = "puffsLeft";

    protected float puffSeconds = 1.6f;
    protected int effectDurationSec = 600;
    protected float baseStrength = 1f;
    protected Dictionary<string, float> effects = new() { ["hungerrate"] = -0.25f };

    protected float exhaleScale = 1f;
    protected bool crackle;

    SimpleParticleProperties? drawSmoke;
    SimpleParticleProperties? exhaleSmoke;
    SimpleParticleProperties? sparks;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (Attributes != null)
        {
            puffSeconds = Attributes["puffSeconds"].AsFloat(puffSeconds);
            effectDurationSec = Attributes["effectDurationSec"].AsInt(effectDurationSec);
            baseStrength = Attributes["strength"].AsFloat(baseStrength);
            effects = Attributes["effects"].AsObject<Dictionary<string, float>>() ?? effects;
            exhaleScale = Attributes["exhaleScale"].AsFloat(exhaleScale);
            crackle = Attributes["crackle"].AsBool(crackle);
        }

        drawSmoke = MakeSmoke(1, 2, 0.15f, 0.4f);
        exhaleSmoke = MakeSmoke(10 * exhaleScale, 16 * exhaleScale, 0.3f * exhaleScale, 0.9f * exhaleScale);

        // Kretek: clove cigarettes crackle and spit tiny embers as they burn
        sparks = new SimpleParticleProperties(
            0, 1,
            ColorUtil.ToRgba(255, 255, 160, 60),
            new Vec3d(), new Vec3d(0.03, 0.03, 0.03),
            new Vec3f(-0.4f, 0.1f, -0.4f), new Vec3f(0.8f, 0.6f, 0.8f),
            0.3f, 1f, 0.05f, 0.1f,
            EnumParticleModel.Cube
        );
        sparks.VertexFlags = 255;
    }

    static SimpleParticleProperties MakeSmoke(float minQ, float maxQ, float minSize, float maxSize)
    {
        var p = new SimpleParticleProperties(
            minQ, maxQ,
            ColorUtil.ToRgba(110, 200, 200, 200),
            new Vec3d(), new Vec3d(),
            new Vec3f(-0.05f, 0.05f, -0.05f), new Vec3f(0.05f, 0.15f, 0.05f),
            2.5f, -0.01f, minSize, maxSize,
            EnumParticleModel.Quad
        );
        p.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 0.8f);
        p.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -100f);
        p.WindAffected = true;
        return p;
    }

    protected abstract int MaxPuffs(ItemStack stack);

    protected virtual float Strength(ItemStack stack) => baseStrength;

    /// <summary>Whether there is anything to smoke (a pipe needs to be packed first).</summary>
    protected virtual bool HasSomethingToSmoke(ItemStack stack) => true;

    /// <summary>Lang code suffix of the error shown when it can't be smoked right now, or null if it can.</summary>
    protected virtual string? WhyCantSmoke(ItemStack stack) => HasSomethingToSmoke(stack) ? null : "empty";

    /// <summary>Burning things need a light first; a vape doesn't.</summary>
    protected virtual bool NeedsLighting => true;

    /// <summary>Called on the server after every puff, before the puff counter goes down.</summary>
    protected virtual void OnPuffTaken(ItemSlot slot, EntityAgent byEntity) { }

    /// <summary>Lets a subclass use right click for something else first, like packing a pipe.</summary>
    protected virtual bool TryAlternateInteract(ItemSlot slot, EntityAgent byEntity) => false;

    /// <summary>Called on the server after the last puff.</summary>
    protected abstract void OnSmokedOut(ItemSlot slot, EntityAgent byEntity);

    protected static bool IsLit(ItemStack stack) => stack.Attributes.GetBool(LitAttr);

    protected int PuffsLeft(ItemStack stack) => stack.Attributes.GetInt(PuffsAttr, MaxPuffs(stack));

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!firstEvent || slot.Itemstack == null) return;

        if (TryAlternateInteract(slot, byEntity))
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        string? reason = WhyCantSmoke(slot.Itemstack);
        if (reason != null)
        {
            ShowError(byEntity, reason);
            return;
        }

        if (NeedsLighting && !IsLit(slot.Itemstack))
        {
            if (!TryIgnite(byEntity))
            {
                ShowError(byEntity, "needlight");
                return;
            }

            if (byEntity.World.Side == EnumAppSide.Server)
            {
                SplitOffOne(slot, byEntity, stack => stack.Attributes.SetBool(LitAttr, true));
            }
        }

        handling = EnumHandHandling.PreventDefault;
    }

    /// <summary>
    /// Lighting or puffing changes the stack's attributes, so only one item of a stack
    /// should be affected. Keep one in hand, change it, then hand the untouched rest back
    /// (changing it first stops the rest from merging straight back into the hand slot).
    /// </summary>
    protected static void SplitOffOne(ItemSlot slot, EntityAgent byEntity, System.Action<ItemStack> change)
    {
        if (slot.Itemstack == null) return;

        ItemStack? rest = null;
        if (slot.Itemstack.StackSize > 1)
        {
            rest = slot.Itemstack.Clone();
            rest.StackSize = slot.Itemstack.StackSize - 1;
            slot.Itemstack.StackSize = 1;
        }

        change(slot.Itemstack);
        slot.MarkDirty();

        if (rest != null && !byEntity.TryGiveItemStack(rest))
        {
            byEntity.World.SpawnItemEntity(rest, byEntity.Pos.XYZ);
        }
    }

    /// <summary>
    /// A glowing item (torch, lantern, lit cigarette) lights for free.
    /// Matches and lighters (attribute cigarettesIgniter) lose one durability.
    /// </summary>
    static bool TryIgnite(EntityAgent byEntity)
    {
        ItemSlot? offhand = byEntity.LeftHandItemSlot;
        ItemStack? stack = offhand?.Itemstack;
        if (offhand == null || stack == null) return false;

        bool server = byEntity.World.Side == EnumAppSide.Server;
        var achievements = server ? byEntity.Api.ModLoader.GetModSystem<AchievementSystem>() : null;

        if (stack.Collectible is ItemSmokable && IsLit(stack))
        {
            achievements?.OnLit(byEntity, "chain");
            return true;
        }

        if (stack.Collectible.LightHsv != null && stack.Collectible.LightHsv[2] > 0)
        {
            achievements?.OnLit(byEntity, "light");
            return true;
        }

        if (stack.Collectible.Attributes?["cigarettesIgniter"].AsBool() == true)
        {
            if (server)
            {
                // "matches" or "lighter", whatever the igniter is called
                achievements?.OnLit(byEntity, stack.Collectible.FirstCodePart());
                stack.Collectible.DamageItem(byEntity.World, byEntity, offhand, 1);
            }
            return true;
        }

        return false;
    }

    protected static void ShowError(EntityAgent byEntity, string code)
    {
        (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(byEntity, code, Lang.Get("cigarettes:ingameerror-" + code));
    }

    Vec3d MouthPos(EntityAgent byEntity)
    {
        return byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.1, 0)
            .Ahead(0.35, byEntity.Pos.Pitch, byEntity.Pos.Yaw);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.World.Side == EnumAppSide.Client && drawSmoke != null && secondsUsed > 0.4f)
        {
            drawSmoke.MinPos = MouthPos(byEntity);
            drawSmoke.AddPos = new Vec3d(0.05, 0.05, 0.05);
            byEntity.World.SpawnParticles(drawSmoke);

            if (crackle && sparks != null && byEntity.World.Rand.NextDouble() < 0.15)
            {
                sparks.MinPos = MouthPos(byEntity).Ahead(0.1, byEntity.Pos.Pitch, byEntity.Pos.Yaw);
                sparks.MinQuantity = 2;
                byEntity.World.SpawnParticles(sparks);
            }
        }

        return secondsUsed < puffSeconds;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (secondsUsed < puffSeconds - 0.05f || slot.Itemstack == null) return;

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            // Exhale: a bigger cloud drifting forward
            if (exhaleSmoke != null)
            {
                exhaleSmoke.MinPos = MouthPos(byEntity);
                exhaleSmoke.AddPos = new Vec3d(0.2, 0.1, 0.2);
                Vec3f fwd = byEntity.Pos.GetViewVector() * 0.2f;
                exhaleSmoke.MinVelocity = fwd;
                exhaleSmoke.AddVelocity = new Vec3f(0.05f, 0.1f, 0.05f);
                byEntity.World.SpawnParticles(exhaleSmoke);
            }
            return;
        }

        byEntity.Api.ModLoader.GetModSystem<SmokingSystem>()
            .ApplyPuff(byEntity, effects, Strength(slot.Itemstack), effectDurationSec);
        OnPuffTaken(slot, byEntity);

        var achievements = byEntity.Api.ModLoader.GetModSystem<AchievementSystem>();
        achievements?.OnPuff(byEntity, this);

        int left = PuffsLeft(slot.Itemstack) - 1;
        if (left > 0)
        {
            slot.Itemstack.Attributes.SetInt(PuffsAttr, left);
            slot.MarkDirty();
            return;
        }

        achievements?.OnSmokedOut(byEntity, this);
        OnSmokedOut(slot, byEntity);
        slot.MarkDirty();
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        return true;
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        string name = base.GetHeldItemName(itemStack);
        return NeedsLighting && IsLit(itemStack) ? Lang.Get("cigarettes:lit-name", name) : name;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        ItemStack? stack = inSlot.Itemstack;
        if (stack == null || !HasSomethingToSmoke(stack)) return;

        dsc.AppendLine(Lang.Get("cigarettes:puffs-left", PuffsLeft(stack), MaxPuffs(stack)));
        dsc.AppendLine(Lang.Get("cigarettes:strength", Strength(stack).ToString("0.0#")));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return new WorldInteraction[]
        {
            new WorldInteraction
            {
                ActionLangCode = "cigarettes:heldhelp-smoke",
                MouseButton = EnumMouseButton.Right,
            }
        }.Append(base.GetHeldInteractionHelp(inSlot));
    }
}
