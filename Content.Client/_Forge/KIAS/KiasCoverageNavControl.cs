using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared._Forge.KIAS;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._Forge.KIAS;

public sealed class KiasCoverageNavControl : ShuttleNavControl
{
    private KiasCoverageGeometry? _geometry;
    private readonly Vector2[] _vertices = new Vector2[67];

    public void SetCoverage(KiasCoverageGeometry? geometry)
    {
        _geometry = geometry;
        if (geometry == null || !EntManager.TryGetEntity(geometry.Target, out var target) || target is not { } uid) return;
        SetMatrix(new EntityCoordinates(uid, Vector2.Zero), Angle.Zero);
        ShowIFF = false;
        ShowDocks = false;
        WorldMinRange = 8;
        WorldMaxRange = Math.Max(32, geometry.Radius * 1.2f);
        ActualRadarRange = WorldMaxRange;
    }

    protected override void DrawSensorCoverage(DrawingHandleScreen handle, Matrix3x2 worldToView, MapId mapId)
    {
        if (_geometry is not { } geometry || !EntManager.TryGetEntity(geometry.Target, out var target) || target is not { } uid
            || !EntManager.TryGetComponent<TransformComponent>(uid, out var transform) || transform.MapID != mapId) return;
        var transforms = EntManager.System<SharedTransformSystem>();
        var center = transforms.GetWorldPosition(uid);
        var direction = transforms.GetWorldRotation(uid).ToWorldVec();
        _vertices[0] = Vector2.Transform(center, worldToView);
        for (var i = 0; i <= 65; i++)
        {
            var angle = Angle.FromDegrees(-geometry.Arc / 2 + geometry.Arc * i / 65);
            var point = center + angle.RotateVec(direction) * geometry.Radius;
            _vertices[i + 1] = Vector2.Transform(point, worldToView);
        }
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _vertices, Color.Cyan.WithAlpha(0.2f));
        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _vertices, Color.Cyan.WithAlpha(0.8f));
    }
}
