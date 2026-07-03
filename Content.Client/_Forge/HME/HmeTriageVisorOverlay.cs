using System.Numerics;
using Content.Client.StatusIcon;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Forge.HME;

public sealed class HmeTriageVisorOverlay : Robust.Client.Graphics.Overlay
{
    private const float IconSizePixels = 8f;
    private const float IconMarginPixels = 1f;

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly StatusIconSystem _statusIcon;
    private readonly HmeTriageVisorSystem _triageVisor;
    private readonly ShaderInstance _unshadedShader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public HmeTriageVisorOverlay()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _statusIcon = _entity.System<StatusIconSystem>();
        _triageVisor = _entity.System<HmeTriageVisorSystem>();
        _unshadedShader = _prototype.Index<ShaderPrototype>("unshaded").Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_triageVisor.RefreshActiveVisors())
            return;

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var xformQuery = _entity.GetEntityQuery<TransformComponent>();
        var metaQuery = _entity.GetEntityQuery<MetaDataComponent>();
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);
        var iconSize = IconSizePixels / EyeManager.PixelsPerMeter;
        var iconMargin = IconMarginPixels / EyeManager.PixelsPerMeter;

        var query = _entity.EntityQueryEnumerator<MobStateComponent, DamageableComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var damageable, out var sprite, out var xform))
        {
            var meta = metaQuery.GetComponent(uid);

            if (xform.MapID != args.MapId ||
                !sprite.Visible ||
                meta.EntityLifeStage >= EntityLifeStage.Terminating ||
                !_triageVisor.TryGetTriageIcon(damageable, out var icon) ||
                !_statusIcon.IsVisible((uid, meta), icon))
            {
                continue;
            }

            var bounds = _entity.GetComponentOrNull<StatusIconComponent>(uid)?.Bounds ?? sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var matty = Matrix3x2.Multiply(rotationMatrix, worldMatrix);
            handle.SetTransform(matty);
            handle.UseShader(_unshadedShader);

            var texture = _sprite.GetFrame(icon.Icon, _timing.RealTime);
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - iconSize - iconMargin;
            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - iconSize - iconMargin;
            var box = Box2.FromDimensions(new Vector2(xOffset, yOffset), new Vector2(iconSize, iconSize));

            handle.DrawTextureRect(texture, box);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
