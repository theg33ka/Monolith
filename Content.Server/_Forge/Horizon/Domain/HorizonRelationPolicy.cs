using Content.Shared._Forge.Horizon;

namespace Content.Server._Forge.Horizon.Domain;

public static class HorizonRelationPolicy
{
    public static HorizonAccessTier AccessFor(int contribution, int damage)
    {
        if (damage >= 500)
            return HorizonAccessTier.Basic;

        var effective = Math.Max(0, contribution - damage * 2);
        return effective switch
        {
            >= 1000 => HorizonAccessTier.Integrated,
            >= 500 => HorizonAccessTier.Partner,
            >= 100 => HorizonAccessTier.Operator,
            _ => HorizonAccessTier.Basic,
        };
    }
}
