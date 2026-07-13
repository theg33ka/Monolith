using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.Horizon;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonPerformanceEnvelopeTests
{
    [Test]
    public void TypicalClusterSoakKeepsEveryCollectionAndSearchBounded()
    {
        const int capacity = 64;
        var state = new HorizonState();
        state.Reset(10000);
        Assert.That(state.WorkQueue.Capacity, Is.EqualTo(256));

        var queue = new BoundedWorkQueue<int>(capacity);
        for (var index = 0; index < capacity * 2; index++)
            queue.TryEnqueue(index);

        Assert.Multiple(() =>
        {
            Assert.That(queue.Count, Is.EqualTo(capacity));
            Assert.That(queue.Rejected, Is.EqualTo(capacity));
        });

        var map = new MapId(1);
        var objects = Enumerable.Range(0, 32)
            .Select(index => new HorizonSpatialObject(
                new Vector2(MathF.Cos(index) * 2500f, MathF.Sin(index) * 2500f),
                index % 3))
            .ToArray();
        var zones = Enumerable.Range(0, 16)
            .Select(index => new HorizonProtectedZone(
                map,
                new Vector2(MathF.Cos(index * 2f) * 7000f, MathF.Sin(index * 2f) * 7000f),
                250f,
                index % 2 == 0,
                null))
            .ToArray();
        var projects = Enumerable.Range(0, 12)
            .Select(index => new HorizonProjectCandidate(
                $"project-{index}",
                index % 2 == 0 ? HorizonObjectKind.Energy : HorizonObjectKind.Mining,
                index,
                1,
                1,
                10,
                5,
                5))
            .ToArray();
        var ledger = new HorizonLedger { Raw = 1000, Components = 1000, Energy = 1000 };
        var stopwatch = Stopwatch.StartNew();
        for (var cycle = 0; cycle < 500; cycle++)
        {
            _ = HorizonSpatialPolicy.FindPlacement(
                Vector2.Zero,
                objects,
                zones,
                map,
                500f,
                4000f,
                8000f,
                250f,
                10000f,
                3,
                64);
            _ = HorizonPlanningPolicy.SelectNext(
                projects,
                new Dictionary<HorizonObjectKind, int>(),
                ledger);
            _ = HorizonDefensePolicy.CanChase(Vector2.Zero, new Vector2(1000f, 1000f), 5000f);
        }
        stopwatch.Stop();

        TestContext.Progress.WriteLine($"Horizon 500-cycle domain soak: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
    }
}
