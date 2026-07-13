using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.Horizon;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonRelationPolicyTests
{
    [TestCase(0, 0, HorizonAccessTier.Basic)]
    [TestCase(100, 0, HorizonAccessTier.Operator)]
    [TestCase(500, 0, HorizonAccessTier.Partner)]
    [TestCase(1000, 0, HorizonAccessTier.Integrated)]
    [TestCase(1000, 500, HorizonAccessTier.Basic)]
    public void AccessUsesContributionAndDamage(int contribution, int damage, HorizonAccessTier expected)
    {
        Assert.That(HorizonRelationPolicy.AccessFor(contribution, damage), Is.EqualTo(expected));
    }
}
