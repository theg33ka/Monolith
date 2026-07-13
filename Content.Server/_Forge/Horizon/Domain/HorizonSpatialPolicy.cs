using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._Forge.Horizon.Domain;

public readonly record struct HorizonSpatialObject(Vector2 Position, int BranchDepth);

public readonly record struct HorizonPlacement(Vector2 Position, int BranchDepth, float Score);

public static class HorizonSpatialPolicy
{
    private const float GoldenAngle = 2.3999632f;

    public static HorizonPlacement? FindPlacement(
        Vector2 anchor,
        IReadOnlyList<HorizonSpatialObject> objects,
        IReadOnlyList<HorizonProtectedZone> protectedZones,
        MapId mapId,
        float minDistance,
        float preferredDistance,
        float maxDistance,
        float projectRadius,
        float bubbleRadius,
        int maxBranchDepth,
        int candidateLimit)
    {
        var count = Math.Clamp(candidateLimit, 1, 64);
        var minimum = Math.Max(1f, minDistance);
        var preferred = Math.Clamp(preferredDistance, minimum, Math.Max(minimum, maxDistance));
        var maximum = Math.Max(preferred, maxDistance);
        var bubble = Math.Max(maximum, bubbleRadius);
        HorizonPlacement? best = null;

        for (var index = 0; index < count; index++)
        {
            var ring = index % 3;
            var radius = ring switch
            {
                0 => preferred,
                1 => (minimum + preferred) / 2f,
                _ => (preferred + maximum) / 2f,
            };
            var angle = index * GoldenAngle;
            var position = anchor + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            if (Vector2.DistanceSquared(anchor, position) > bubble * bubble)
                continue;

            var nearestDistance = float.MaxValue;
            var nearestDepth = 0;
            var links = 0;
            var invalid = false;
            foreach (var obj in objects)
            {
                var distance = Vector2.Distance(position, obj.Position);
                if (distance < minimum)
                {
                    invalid = true;
                    break;
                }

                if (distance <= maximum)
                    links++;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestDepth = obj.BranchDepth;
                }
            }

            var requiredLinks = objects.Count <= 3 ? Math.Min(1, objects.Count) : 2;
            if (invalid || links < requiredLinks)
                continue;

            var branchDepth = nearestDepth + 1;
            if (branchDepth > Math.Max(1, maxBranchDepth))
                continue;

            var penalty = MathF.Abs(nearestDistance - preferred);
            foreach (var zone in protectedZones)
            {
                if (zone.MapId != mapId)
                    continue;

                var clearance = Vector2.Distance(position, zone.Position) - zone.Radius - projectRadius;
                if (zone.Hard && clearance < 0f)
                {
                    invalid = true;
                    break;
                }

                if (!zone.Hard && clearance < minimum)
                    penalty += minimum - clearance;
            }

            if (invalid)
                continue;

            var placement = new HorizonPlacement(position, branchDepth, penalty);
            if (best is null || placement.Score < best.Value.Score)
                best = placement;
        }

        return best;
    }
}
