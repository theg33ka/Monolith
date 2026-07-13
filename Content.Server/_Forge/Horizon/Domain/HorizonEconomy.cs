namespace Content.Server._Forge.Horizon.Domain;

public static class HorizonEconomy
{
    public static void ApplyCycle(HorizonLedger ledger, HorizonAggregates aggregates, int resourceCap)
    {
        var cap = Math.Max(0, resourceCap);
        ledger.Raw = Math.Clamp(ledger.Raw + Math.Max(0, aggregates.RawIncome), 0, cap);
        ledger.Components = Math.Clamp(ledger.Components + Math.Max(0, aggregates.ProductionCapacity), 0, cap);

        var energyGain = aggregates.EnergyCapacity <= 0
            ? 0
            : Math.Max(1, aggregates.EnergyCapacity / 20);
        var energyCap = Math.Min(cap, Math.Max(0, aggregates.EnergyCapacity));
        ledger.Energy = Math.Clamp(ledger.Energy + energyGain, 0, energyCap);
    }

    public static bool TrySpend(HorizonLedger ledger, int raw, int components, int energy)
    {
        raw = Math.Max(0, raw);
        components = Math.Max(0, components);
        energy = Math.Max(0, energy);
        if (ledger.Raw < raw || ledger.Components < components || ledger.Energy < energy)
            return false;

        ledger.Raw -= raw;
        ledger.Components -= components;
        ledger.Energy -= energy;
        return true;
    }

    public static void Refund(HorizonLedger ledger, int raw, int components, int energy, int resourceCap)
    {
        var cap = Math.Max(0, resourceCap);
        ledger.Raw = Math.Clamp(ledger.Raw + Math.Max(0, raw), 0, cap);
        ledger.Components = Math.Clamp(ledger.Components + Math.Max(0, components), 0, cap);
        ledger.Energy = Math.Clamp(ledger.Energy + Math.Max(0, energy), 0, cap);
    }
}
