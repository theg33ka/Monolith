#pragma warning disable RA0002
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Collections;
using System.Reflection;
using Content.Server._Forge.KIAS;
using Content.Server._Forge.KIAS.Controllers;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared._Forge.KIAS.Controllers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasControllerRuntimeTests
{
    [Test]
    public async Task DirtyTopologyTemporarilyChangesAvailabilityWithoutStoppingMachine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        var runtime = em.System<KiasControllerRuntimeSystem>();
        EntityUid card = default;
        await pair.Server.WaitAssertion(() =>
        {
            EntityUid Spawn(string prototype)
            {
                var uid = em.SpawnEntity(prototype, new EntityCoordinates(map.Grid, .5f, .5f));
                em.System<SharedPowerReceiverSystem>().SetNeedsPower(uid, false);
                return uid;
            }
            Spawn("KiasCore"); Spawn("KiasDataCable");
            var rack = Spawn("KiasControllerRack");
            card = Spawn("KiasProgrammableController");
            var program = new KiasControllerProgram { Name = "Topology lifetime" };
            program.Nodes.Add(new KiasControllerNode { Id = 1, Kind = KiasNodeKind.BoolConstant, Config = new() { Bool = true } });
            em.GetComponent<KiasControllerCardComponent>(card).Program = program;
            em.System<KiasSystem>().Rebuild(map.Grid);
            Assert.That(em.System<ItemSlotsSystem>().TryInsert(rack, KiasControllerRackComponent.SlotId(0), card, null), Is.True);
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(runtime.Running(card), Is.True);
            var states = (IDictionary) typeof(KiasControllerRuntimeSystem).GetField("_cards", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
            var state = states[card]!;
            var machine = (KiasGraphMachine) state.GetType().GetField("Machine")!.GetValue(state)!;
            em.System<KiasSystem>().Invalidate(map.Grid);
            Assert.That(runtime.Running(card), Is.False);
            Assert.That(machine.Active, Is.True);
            Assert.That(runtime.Fault(card), Is.Empty);
            em.System<KiasSystem>().Rebuild(map.Grid);
            Assert.That(runtime.Running(card), Is.True);
            Assert.That(states[card], Is.SameAs(state));
            Assert.That(state.GetType().GetField("Machine")!.GetValue(state), Is.SameAs(machine));
        });
        await pair.CleanReturnAsync();
    }

    private static KiasControllerProgram Warning(EntityUid specific)
    {
        var program = new KiasControllerProgram { Name = "Portable warning" };
        program.Nodes.AddRange(new KiasControllerNode[]
        {
            new() { Id = 1, Kind = KiasNodeKind.Any, Profile = "WeaponFlashDetector" },
            new() { Id = 2, Kind = KiasNodeKind.EnumConstant, Config = new() { Enum = (int) KiasContactDisposition.Hostile } },
            new() { Id = 3, Kind = KiasNodeKind.EnumCompare }, new() { Id = 4, Kind = KiasNodeKind.If },
            new() { Id = 5, Kind = KiasNodeKind.All, Profile = "Recorder" },
            new() { Id = 6, Kind = KiasNodeKind.StringConstant, Config = new() { Text = "portable-accepted" } },
            new() { Id = 7, Kind = KiasNodeKind.Specific, Profile = "Recorder", Binding = specific },
            new() { Id = 8, Kind = KiasNodeKind.Counter },
        });
        void W(int from, string output, int to, string input) => program.Wires.Add(new()
            { FromNode = from, FromPort = output, ToNode = to, ToPort = input });
        W(1, "Disposition", 3, "A"); W(2, "Value", 3, "B"); W(3, "Value", 4, "Condition");
        W(1, "Triggered", 4, "Trigger"); W(4, "True", 5, "Record"); W(4, "True", 7, "Record");
        W(6, "Value", 5, "Message"); W(6, "Value", 7, "Message"); W(1, "Triggered", 8, "Increment");
        return program;
    }

    [Test]
    public async Task PortableSelectorsSpecificIsolationAndPhysicalLifecycle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid core = default, rack = default, otherRack = default, card = default, cable = default;
        EntityUid otherGrid = default;
        var detectors = new List<EntityUid>();
        var recorders = new List<EntityUid>();
        var otherRecorders = new List<EntityUid>();
        EntityUid otherDetector = default;
        var runtime = em.System<KiasControllerRuntimeSystem>();
        var io = em.System<KiasControllerIoSystem>();
        var slots = em.System<ItemSlotsSystem>();
        await pair.Server.WaitAssertion(() =>
        {
            var maps = em.System<SharedMapSystem>();
            for (var x = 0; x < 6; x++) maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
            EntityUid Spawn(string prototype, EntityUid grid)
            {
                var uid = em.SpawnEntity(prototype, new EntityCoordinates(grid, 0.5f, 0.5f));
                em.System<SharedPowerReceiverSystem>().SetNeedsPower(uid, false);
                return uid;
            }
            core = Spawn("KiasCore", map.Grid);
            cable = Spawn("KiasDataCable", map.Grid);
            rack = Spawn("KiasControllerRack", map.Grid);
            for (var i = 0; i < 3; i++)
            {
                detectors.Add(Spawn("KiasWeaponFlashDetector", map.Grid));
                recorders.Add(Spawn("KiasRecorder", map.Grid));
            }
            card = Spawn("KiasProgrammableController", map.Grid);
            em.GetComponent<KiasControllerCardComponent>(card).Program = Warning(recorders[0]);
            var kias = em.System<KiasSystem>();
            kias.Rebuild(map.Grid);
            Assert.That(runtime.Running(card), Is.False);
            Assert.That(slots.TryInsert(rack, KiasControllerRackComponent.SlotId(0), card, null), Is.True);
            var second = maps.CreateGridEntity(map.MapId);
            otherGrid = second.Owner;
            maps.SetTile(second, second.Comp, Vector2i.Zero, map.Tile.Tile);
            em.System<SharedTransformSystem>().SetLocalPosition(second, new Vector2(100, 0));
            Spawn("KiasCore", second);
            Spawn("KiasDataCable", second);
            otherRack = Spawn("KiasControllerRack", second);
            otherDetector = Spawn("KiasWeaponFlashDetector", second);
            for (var i = 0; i < 2; i++) otherRecorders.Add(Spawn("KiasRecorder", second));
            kias.Rebuild(second);
        });
        await pair.RunTicksSync(40);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(runtime.Running(card), Is.True, runtime.Fault(card));
            Assert.That(runtime.LastValue(card, 1, "$MatchedCount").Number, Is.EqualTo(3));
            foreach (var detector in detectors)
            {
                io.Emit(detector, "WeaponFlashDetector", "Disposition", KiasGraphValue.Enumeration((int) KiasContactDisposition.Hostile));
                io.Emit(detector, "WeaponFlashDetector", "Triggered", KiasGraphValue.Pulse);
            }
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(runtime.LastValue(card, 8, "Value").Number, Is.EqualTo(3));
            foreach (var recorder in recorders)
                Assert.That(em.GetComponent<KiasRecorderComponent>(recorder).Entries, Has.Some.Contains("portable-accepted"));
            Assert.That(slots.TryEject(rack, KiasControllerRackComponent.SlotId(0), null, out _), Is.True);
            Assert.That(runtime.Running(card), Is.False);
            Assert.That(slots.TryInsert(otherRack, KiasControllerRackComponent.SlotId(0), card, null), Is.True);
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(runtime.LastValue(card, 1, "$MatchedCount").Number, Is.EqualTo(1));
            Assert.That(runtime.LastValue(card, 7, "$MatchedCount").Number, Is.Zero);
            Assert.That(runtime.LastValue(card, 8, "Value").Number, Is.Zero);
            io.Emit(otherDetector, "WeaponFlashDetector", "Disposition", KiasGraphValue.Enumeration((int) KiasContactDisposition.Hostile));
            io.Emit(otherDetector, "WeaponFlashDetector", "Triggered", KiasGraphValue.Pulse);
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            foreach (var recorder in otherRecorders)
                Assert.That(em.GetComponent<KiasRecorderComponent>(recorder).Entries, Has.Some.Contains("portable-accepted"));
            Assert.That(runtime.LastValue(card, 8, "Value").Number, Is.EqualTo(1));
            Assert.That(slots.TryEject(otherRack, KiasControllerRackComponent.SlotId(0), null, out _), Is.True);
            Assert.That(slots.TryInsert(rack, KiasControllerRackComponent.SlotId(0), card, null), Is.True);
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            em.DeleteEntity(cable);
            em.System<KiasSystem>().Rebuild(map.Grid);
            Assert.That(runtime.Running(card), Is.False);
            cable = em.SpawnEntity("KiasDataCable", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            em.System<KiasSystem>().Rebuild(map.Grid);
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(runtime.Running(card), Is.True);
            Assert.That(runtime.LastValue(card, 8, "Value").Number, Is.Zero);
            em.System<SharedPowerReceiverSystem>().SetPowerDisabled(rack, true);
        });
        await pair.RunTicksSync(40);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(runtime.Running(card), Is.False);
            em.System<SharedPowerReceiverSystem>().SetPowerDisabled(rack, false);
        });
        await pair.RunTicksSync(40);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(runtime.Running(card), Is.True);
            em.System<KiasSystem>().SetEnabled(core, false);
            Assert.That(runtime.Running(card), Is.False);
            em.System<KiasSystem>().SetEnabled(core, true);
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() => Assert.That(runtime.Running(card), Is.True));
        await pair.CleanReturnAsync();
    }
}
