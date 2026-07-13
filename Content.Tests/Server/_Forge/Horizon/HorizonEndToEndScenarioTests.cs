using System.Numerics;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.Horizon;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class HorizonEndToEndScenarioTests
{
    [Test]
    public void MvpPoliciesCompleteOneLifecycleAndPermanentlyStop()
    {
        var state = new HorizonState();
        state.Reset(8);
        int[] rtrs = [1, 2, 3, 4, 5, 6, 7, 8];
        var positions = rtrs.ToDictionary(value => value, value => new Vector2(value * value * 100f, value * 50f));
        var neighbor = HorizonDeploymentPlanner.FindNearestNeighbor(1, rtrs, value => positions[value]);
        Assert.That(neighbor, Is.EqualTo(2));

        state.Phase = HorizonDeploymentPhase.Deploying;
        Assert.Multiple(() =>
        {
            Assert.That(HorizonRecoveryPolicy.Select(1, false), Is.EqualTo(HorizonRecoveryAction.RetryAms));
            Assert.That(HorizonRecoveryPolicy.Select(2, false), Is.EqualTo(HorizonRecoveryAction.RetryAms));
            Assert.That(HorizonRecoveryPolicy.Select(3, false), Is.EqualTo(HorizonRecoveryAction.RelocateCluster));
            Assert.That(HorizonRecoveryPolicy.Select(3, true), Is.EqualTo(HorizonRecoveryAction.TerminateCycle));
        });

        state.Phase = HorizonDeploymentPhase.Operational;
        state.MatureNetwork = true;
        state.ActiveCluster = "HZ-01";
        state.Aggregates.RawIncome = 12;
        state.Aggregates.ProductionCapacity = 10;
        state.Aggregates.EnergyCapacity = 500;
        HorizonEconomy.ApplyCycle(state.Ledger, state.Aggregates, 1000);
        var next = HorizonPlanningPolicy.SelectNext(
            [
                new HorizonProjectCandidate("HorizonE01", HorizonObjectKind.Energy, 10, 1, 1, 10, 5, 5),
                new HorizonProjectCandidate("HorizonD04", HorizonObjectKind.Mining, 20, 1, 1, 30, 10, 5),
            ],
            state.ObjectCounts,
            state.Ledger);
        Assert.That(next?.ProjectId, Is.EqualTo("HorizonE01"));

        var map = new MapId(1);
        var placement = HorizonSpatialPolicy.FindPlacement(
            Vector2.Zero,
            [new HorizonSpatialObject(Vector2.Zero, 0)],
            [],
            map,
            1000f,
            4000f,
            7000f,
            500f,
            10000f,
            3,
            16);
        Assert.That(placement, Is.Not.Null);

        var incidentKey = HorizonDefensePolicy.IncidentKey("crew", "O-01");
        state.Incidents[incidentKey] = new HorizonIncident
        {
            Key = incidentKey,
            Organization = "crew",
            Damage = 250,
            FirstSeen = TimeSpan.Zero,
            LastSeen = TimeSpan.Zero,
            ResponseOrdered = true,
        };
        state.Relations["crew"] = new HorizonRelation
        {
            Organization = "crew",
            Contribution = 700,
            Damage = 50,
            Access = HorizonRelationPolicy.AccessFor(700, 50),
            Iff = HorizonDefensePolicy.IffForDamage(250),
        };
        Assert.Multiple(() =>
        {
            Assert.That(state.Relations["crew"].Access, Is.EqualTo(HorizonAccessTier.Partner));
            Assert.That(state.Relations["crew"].Iff, Is.EqualTo(HorizonIffMode.Restricted));
            Assert.That(HorizonWanderingAiPolicy.CanHandoff(
                state.Phase, true, true, true, true, false, true, true), Is.True);
        });

        var command = new HorizonRegisteredObject
        {
            Entity = new EntityUid(100),
            ObjectId = "O-01",
            Kind = HorizonObjectKind.Command,
            MapId = map,
            Active = true,
        };
        state.Objects[command.Entity] = command;
        var order = new HorizonOrder { Status = HorizonOrderStatus.Active };
        state.Orders[order.Id] = order;
        Assert.That(state.WorkQueue.TryEnqueue(new HorizonWorkItem(
            HorizonWorkKind.RunStrategicCycle,
            TimeSpan.Zero)), Is.True);

        Assert.That(HorizonLifecyclePolicy.Destroy(state, "O-01 lost"), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(state.Phase, Is.EqualTo(HorizonDeploymentPhase.Destroyed));
            Assert.That(state.MatureNetwork, Is.False);
            Assert.That(state.Objects.Values.All(value => !value.Active && !value.Dormant), Is.True);
            Assert.That(state.Orders[order.Id].Status, Is.EqualTo(HorizonOrderStatus.Cancelled));
            Assert.That(state.WorkQueue.Count, Is.Zero);
            Assert.That(state.Aggregates.ActiveObjects, Is.Zero);
            Assert.That(state.Incidents[incidentKey].ResponseOrdered, Is.False);
            Assert.That(HorizonLifecyclePolicy.Destroy(state, "retry"), Is.False);
        });
    }
}
