using Content.Server._Forge.Horizon.Domain;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonRecoveryPolicyTests
{
    [TestCase(1, false, HorizonRecoveryAction.RetryAms)]
    [TestCase(2, false, HorizonRecoveryAction.RetryAms)]
    [TestCase(3, false, HorizonRecoveryAction.RelocateCluster)]
    [TestCase(1, true, HorizonRecoveryAction.RetryAms)]
    [TestCase(2, true, HorizonRecoveryAction.RetryAms)]
    [TestCase(3, true, HorizonRecoveryAction.TerminateCycle)]
    public void RecoverySequenceIsBounded(int attempts, bool emergencyUsed, HorizonRecoveryAction expected)
    {
        Assert.That(HorizonRecoveryPolicy.Select(attempts, emergencyUsed), Is.EqualTo(expected));
    }
}
