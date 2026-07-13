using System.Collections.Generic;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.Horizon;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonPlanningPolicyTests
{
    [Test]
    public void SelectsAffordableMissingProjectByPriority()
    {
        HorizonProjectCandidate[] candidates =
        [
            new("mining", HorizonObjectKind.Mining, 20, 1, 1, 20, 0, 0),
            new("energy", HorizonObjectKind.Energy, 10, 1, 1, 10, 0, 0),
            new("command", HorizonObjectKind.Command, 1, 1, 1, 0, 0, 0),
        ];
        var ledger = new HorizonLedger { Raw = 15 };

        var selected = HorizonPlanningPolicy.SelectNext(candidates,
            new Dictionary<HorizonObjectKind, int>(), ledger);

        Assert.That(selected?.ProjectId, Is.EqualTo("energy"));
    }

    [Test]
    public void DoesNotPlanSatisfiedKind()
    {
        HorizonProjectCandidate[] candidates =
        [
            new("energy", HorizonObjectKind.Energy, 10, 1, 1, 10, 0, 0),
        ];
        var counts = new Dictionary<HorizonObjectKind, int> { [HorizonObjectKind.Energy] = 1 };

        Assert.That(HorizonPlanningPolicy.SelectNext(candidates, counts, new HorizonLedger { Raw = 100 }), Is.Null);
    }
}
