using Content.Client.Eye.Blinding;
using Content.Client.GameTicking.Managers;
using Content.Shared.Eye.Blinding.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Inventory.Events;
using Content.Shared.Flash.Components;

namespace Content.Client._Starlight.Overlay.Thermal;

public sealed class ThermalVisionSystem : SharedThermalVisionSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private ThroughWallsVisionOverlay _throughWallsOverlay = default!;
    private ThermalVisionOverlay _overlay = default!;

    private EntityUid? _effect = null;
    private readonly EntProtoId _effectPrototype = "EffectThermalVision";
    protected override bool IsPredict() => !_timing.IsFirstTimePredicted;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ThermalVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        // SubscribeLocalEvent<ThermalVisionComponent, FlashImmunityChangedEvent>(OnFlashImmunityChanged);

        _throughWallsOverlay = new();
        _overlay = new();
    }

    private void OnFlashImmunityChanged(Entity<ThermalVisionComponent> ent, ref FlashImmunityChangedEvent args)
    {
        if (_player.LocalSession?.AttachedEntity != ent.Owner) return;

        if (args.IsImmune)
        {
            ent.Comp.blockedByFlashImmunity = true;
            RemoveNightVision();
        }
        else
        {
            ent.Comp.blockedByFlashImmunity = false;
            AddNightVision(ent.Owner);
        }
    }

    private void OnPlayerAttached(Entity<ThermalVisionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        if (_effect == null && !ent.Comp.blockedByFlashImmunity)
            AddNightVision(ent.Owner);
    }

    private void OnPlayerDetached(Entity<ThermalVisionComponent> ent, ref LocalPlayerDetachedEvent args)
        => RemoveNightVision();

    protected override void ToggleOn(Entity<ThermalVisionComponent> ent)
    {
        if (_player.LocalEntity != ent.Owner) return;

        //only allow toggling on if not blocked by flash immunity
        if (ent.Comp.blockedByFlashImmunity) return;

        if (_effect == null)
            AddNightVision(ent.Owner);
    }

    protected override void ToggleOff(Entity<ThermalVisionComponent> ent)
    {
        if (_player.LocalEntity != ent.Owner) return;

        RemoveNightVision();
    }

    private void AddNightVision(EntityUid uid)
    {
        _overlayMan.AddOverlay(_throughWallsOverlay);
        _overlayMan.AddOverlay(_overlay);
        _effect = SpawnAttachedTo(_effectPrototype, Transform(uid).Coordinates);
        _xformSys.SetParent(_effect.Value, uid);
    }
    private void RemoveNightVision()
    {
        _overlayMan.RemoveOverlay(_throughWallsOverlay);
        _overlayMan.RemoveOverlay(_overlay);
        Del(_effect);
        _effect = null;
    }
}
