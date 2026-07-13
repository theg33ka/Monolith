using Content.Server._Forge.Horizon.Domain;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonEconomyTests
{
    [Test]
    public void CycleUsesAggregatesAndCapsResources()
    {
        var ledger = new HorizonLedger { Raw = 95, Components = 45, Energy = 1 };
        var aggregates = new HorizonAggregates
        {
            RawIncome = 20,
            ProductionCapacity = 10,
            EnergyCapacity = 100,
        };

        HorizonEconomy.ApplyCycle(ledger, aggregates, 100);

        Assert.Multiple(() =>
        {
            Assert.That(ledger.Raw, Is.EqualTo(100));
            Assert.That(ledger.Components, Is.EqualTo(55));
            Assert.That(ledger.Energy, Is.EqualTo(6));
        });
    }

    [Test]
    public void SpendIsAtomic()
    {
        var ledger = new HorizonLedger { Raw = 10, Components = 5, Energy = 2 };
        Assert.That(HorizonEconomy.TrySpend(ledger, 11, 1, 1), Is.False);
        Assert.That((ledger.Raw, ledger.Components, ledger.Energy), Is.EqualTo((10, 5, 2)));
        Assert.That(HorizonEconomy.TrySpend(ledger, 4, 2, 1), Is.True);
        Assert.That((ledger.Raw, ledger.Components, ledger.Energy), Is.EqualTo((6, 3, 1)));
    }
}
