using System.Numerics;
using Content.Shared._Forge.Horizon;

namespace Content.Server._Forge.Horizon.Domain;

public static class HorizonDefensePolicy
{
    public static string IncidentKey(string organization, string objectId)
    {
        return $"{organization}:{objectId}".ToLowerInvariant();
    }

    public static HorizonIffMode IffForDamage(int damage)
    {
        return damage switch
        {
            >= 1000 => HorizonIffMode.Hostile,
            >= 500 => HorizonIffMode.Unwanted,
            >= 200 => HorizonIffMode.Restricted,
            _ => HorizonIffMode.Neutral,
        };
    }

    public static bool CanChase(Vector2 protectedCenter, Vector2 target, float chaseRadius)
    {
        var radius = Math.Max(0f, chaseRadius);
        return Vector2.DistanceSquared(protectedCenter, target) <= radius * radius;
    }
}
