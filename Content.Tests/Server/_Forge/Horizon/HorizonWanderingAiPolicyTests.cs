using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.Horizon;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonWanderingAiPolicyTests
{
    [Test]
    public void OperationalDesignatedAiCanUseEmptyCarrier()
    {
        Assert.That(HorizonWanderingAiPolicy.CanHandoff(
            HorizonDeploymentPhase.Operational,
            true,
            true,
            true,
            true,
            false,
            true,
            true), Is.True);
    }

    [TestCase(HorizonDeploymentPhase.Dormant)]
    [TestCase(HorizonDeploymentPhase.Deploying)]
    [TestCase(HorizonDeploymentPhase.Destroyed)]
    public void HandoffRequiresOperationalNetwork(HorizonDeploymentPhase phase)
    {
        Assert.That(HorizonWanderingAiPolicy.CanHandoff(
            phase,
            true,
            true,
            true,
            true,
            false,
            true,
            true), Is.False);
    }

    [Test]
    public void HandoffRejectsOccupiedCarrierAndWrongActor()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HorizonWanderingAiPolicy.CanHandoff(
                HorizonDeploymentPhase.Operational,
                false,
                true,
                true,
                true,
                false,
                true,
                true), Is.False);
            Assert.That(HorizonWanderingAiPolicy.CanHandoff(
                HorizonDeploymentPhase.Operational,
                true,
                true,
                true,
                true,
                true,
                true,
                true), Is.False);
        });
    }
}
