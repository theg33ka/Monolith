using System.Numerics;

namespace Content.Server._Forge.Horizon.Domain;

public static class HorizonDeploymentPlanner
{
    public static T? FindNearestNeighbor<T>(T primary, IReadOnlyList<T> candidates, Func<T, Vector2> position)
        where T : struct, IEquatable<T>
    {
        T? result = null;
        var primaryPosition = position(primary);
        var bestDistanceSquared = float.MaxValue;

        foreach (var candidate in candidates)
        {
            if (candidate.Equals(primary))
                continue;

            var distanceSquared = Vector2.DistanceSquared(primaryPosition, position(candidate));
            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            result = candidate;
        }

        return result;
    }
}
