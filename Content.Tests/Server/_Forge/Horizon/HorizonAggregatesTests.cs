using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.Horizon;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonAggregatesTests
{
    [Test]
    public void LifecycleContributionIsSymmetric()
    {
        var aggregates = new HorizonAggregates();
        var obj = new HorizonRegisteredObject
        {
            Entity = new EntityUid(1),
            ObjectId = "D-04",
            Kind = HorizonObjectKind.Mining,
            Active = true,
            RawIncome = 12,
            EnergyCapacity = 3,
            ProductionCapacity = 2,
        };

        aggregates.Add(obj, 1);
        aggregates.Add(obj, -1);

        Assert.Multiple(() =>
        {
            Assert.That(aggregates.ActiveObjects, Is.Zero);
            Assert.That(aggregates.RawIncome, Is.Zero);
            Assert.That(aggregates.EnergyCapacity, Is.Zero);
            Assert.That(aggregates.ProductionCapacity, Is.Zero);
        });
    }
}
