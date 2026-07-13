using System.Collections.Generic;
using System.Numerics;
using Content.Server._Forge.Horizon.Domain;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonDeploymentPlannerTests
{
    [Test]
    public void NearestNeighborExcludesPrimaryAndUsesDistance()
    {
        var candidates = new List<int> { 1, 2, 3, 4 };
        var positions = new Dictionary<int, Vector2>
        {
            [1] = Vector2.Zero,
            [2] = new(100, 0),
            [3] = new(25, 0),
            [4] = new(50, 0),
        };

        var neighbor = HorizonDeploymentPlanner.FindNearestNeighbor(1, candidates, id => positions[id]);

        Assert.That(neighbor, Is.EqualTo(3));
    }
}
