using System.Numerics;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.Horizon;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonDefensePolicyTests
{
    [TestCase(0, HorizonIffMode.Neutral)]
    [TestCase(200, HorizonIffMode.Restricted)]
    [TestCase(500, HorizonIffMode.Unwanted)]
    [TestCase(1000, HorizonIffMode.Hostile)]
    public void DamageEscalatesIff(int damage, HorizonIffMode expected)
    {
        Assert.That(HorizonDefensePolicy.IffForDamage(damage), Is.EqualTo(expected));
    }

    [Test]
    public void ChaseIsLimitedToProtectedAreaRadius()
    {
        Assert.That(HorizonDefensePolicy.CanChase(Vector2.Zero, new Vector2(100f, 0f), 100f), Is.True);
        Assert.That(HorizonDefensePolicy.CanChase(Vector2.Zero, new Vector2(101f, 0f), 100f), Is.False);
    }
}
