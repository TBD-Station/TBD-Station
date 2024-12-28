using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Holopad;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Holopad;

public sealed class HolopadSystem : SharedHolopadSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HolopadHologramComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<HolopadHologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
        SubscribeAllEvent<TypingChangedEvent>(OnTypingChanged);
        SubscribeNetworkEvent<HolopadHologramVisualsUpdateEvent>(OnVisualsUpdate);
    }

    private void OnComponentInit(EntityUid uid, HolopadHologramComponent component, ComponentInit ev)
    {
        UpdateHologramSprite(uid, null);
    }

    private void OnShaderRender(EntityUid uid, HolopadHologramComponent component, BeforePostShaderRenderEvent ev)
    {
        if (ev.Sprite.PostShader == null)
            return;

        ev.Sprite.PostShader.SetParameter("t", (float)_timing.CurTime.TotalSeconds * component.ScrollRate);
    }

    private void OnTypingChanged(TypingChangedEvent ev, EntitySessionEventArgs args)
    {
        var uid = args.SenderSession.AttachedEntity;

        if (!Exists(uid))
            return;

        if (!HasComp<HolopadUserComponent>(uid))
            return;

        var netEv = new HolopadUserTypingChangedEvent(GetNetEntity(uid.Value), ev.IsTyping);
        RaiseNetworkEvent(netEv);
    }

    private void OnVisualsUpdate(HolopadHologramVisualsUpdateEvent ev)
    {
        UpdateHologramSprite(GetEntity(ev.Hologram), GetEntity(ev.Target));
    }

    private void UpdateHologramSprite(EntityUid hologram, EntityUid? target)
    {
        // Get required components
        if (!TryComp<SpriteComponent>(hologram, out var hologramSprite) ||
            !TryComp<HolopadHologramComponent>(hologram, out var holopadhologram))
            return;

        // Remove all sprite layers
        for (int i = hologramSprite.AllLayers.Count() - 1; i >= 0; i--)
            hologramSprite.RemoveLayer(i);

        if (TryComp<SpriteComponent>(target, out var targetSprite))
        {
            // Use the target's holographic avatar (if available)
            if (TryComp<HolographicAvatarComponent>(target, out var targetAvatar))
            {
                for (int i = 0; i < targetAvatar.LayerData.Length; i++)
                {
                    var layer = targetAvatar.LayerData[i];
                    layer.Shader = "unshaded";

                    hologramSprite.AddLayer(targetAvatar.LayerData[i], i);
                }
            }

            // Otherwise copy the target's current physical appearance
            else
            {
                hologramSprite.CopyFrom(targetSprite);
            }
        }

        // There is no target, display a default sprite instead (if available)
        else
        {
            if (string.IsNullOrEmpty(holopadhologram.RsiPath) || string.IsNullOrEmpty(holopadhologram.RsiState))
                return;

            var layer = new PrototypeLayerData();
            layer.RsiPath = holopadhologram.RsiPath;
            layer.State = holopadhologram.RsiState;
            layer.Shader = "unshaded";

            hologramSprite.AddLayer(layer);
        }

        // Override specific values
        hologramSprite.Color = Color.White;
        hologramSprite.Offset = holopadhologram.Offset;
        hologramSprite.Scale = default;
        hologramSprite.DrawDepth = (int)DrawDepth.Mobs;
        hologramSprite.NoRotation = true;
        hologramSprite.DirectionOverride = Direction.South;
        hologramSprite.EnableDirectionOverride = true;

        // Remove shading from all layers
        for (int i = 0; i < hologramSprite.AllLayers.Count(); i++)
            hologramSprite.LayerSetShader(i, "unshaded");

        UpdateHologramShader(hologram, hologramSprite, holopadhologram);
    }

    private void UpdateHologramShader(EntityUid uid, SpriteComponent sprite, HolopadHologramComponent holopadHologram)
    {
        // Find the texture height of the largest layer
        float texHeight = sprite.AllLayers.Max(x => x.PixelSize.Y);

        var instance = _prototypeManager.Index<ShaderPrototype>(holopadHologram.ShaderName).InstanceUnique();
        instance.SetParameter("color1", new Vector3(holopadHologram.Color1.R, holopadHologram.Color1.G, holopadHologram.Color1.B));
        instance.SetParameter("color2", new Vector3(holopadHologram.Color2.R, holopadHologram.Color2.G, holopadHologram.Color2.B));
        instance.SetParameter("alpha", holopadHologram.Alpha);
        instance.SetParameter("intensity", holopadHologram.Intensity);
        instance.SetParameter("texHeight", texHeight);
        instance.SetParameter("t", (float)_timing.CurTime.TotalSeconds * holopadHologram.ScrollRate);

        sprite.PostShader = instance;
        sprite.RaiseShaderEvent = true;
    }
}
