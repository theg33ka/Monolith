using System.Numerics;
using Content.Server._Forge.Horizon.Domain;
using Robust.Shared.Map;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonSpatialPolicyTests
{
    [Test]
    public void CandidateSearchIsBoundedAndAvoidsHardZone()
    {
        var map = new MapId(1);
        HorizonSpatialObject[] objects =
        [
            new(Vector2.Zero, 0),
            new(new Vector2(0f, 2000f), 0),
        ];
        HorizonProtectedZone[] zones =
        [
            new(map, new Vector2(4000f, 0f), 1500f, true, null),
        ];

        var result = HorizonSpatialPolicy.FindPlacement(
            Vector2.Zero,
            objects,
            zones,
            map,
            1000f,
            4000f,
            7000f,
            500f,
            10000f,
            3,
            16);

        Assert.That(result, Is.Not.Null);
        Assert.That(Vector2.Distance(result!.Value.Position, zones[0].Position),
            Is.GreaterThanOrEqualTo(2000f));
        Assert.That(result.Value.BranchDepth, Is.EqualTo(1));
    }

    [Test]
    public void RejectsCandidatesPastBranchLimit()
    {
        HorizonSpatialObject[] objects = [new(Vector2.Zero, 3)];
        var result = HorizonSpatialPolicy.FindPlacement(
            Vector2.Zero,
            objects,
            [],
            new MapId(1),
            100f,
            1000f,
            2000f,
            100f,
            3000f,
            3,
            8);

        Assert.That(result, Is.Null);
    }
}
