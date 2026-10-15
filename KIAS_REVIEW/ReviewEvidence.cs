#pragma warning disable RA0002
using System.Linq;
using Content.Server._Forge.KIAS;
using Content.Server._Forge.KIAS.Controllers;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared._Forge.KIAS.Controllers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

// These evidence tests assert the observed defect, not the desired fixed behavior.
[TestFixture]
public sealed class KiasIndependentReviewEvidence
{
    [Test]
    public async Task RebuildLosesUnchangedBooleanOutput()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            EntityUid Spawn(string id)
            {
                var uid = em.SpawnEntity(id, new EntityCoordinates(map.Grid, .5f, .5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid); return uid;
            }
            Spawn("KiasCore"); Spawn("KiasDataCable"); var adapter = Spawn("KiasDeviceAdapter");
            var kias = em.System<KiasSystem>(); var io = em.System<KiasControllerIoSystem>();
            kias.Rebuild(map.Grid);
            io.Emit(adapter, "DeviceAdapter", "State", KiasGraphValue.Boolean(true));
            Assert.That(io.TryOutput(adapter, "DeviceAdapter", "State", out var before, out _), Is.True);
            Assert.That(before.Bool, Is.True);
            kias.Rebuild(map.Grid);
            Assert.That(kias.IsOnline(adapter), Is.True);
            Assert.That(io.TryOutput(adapter, "DeviceAdapter", "State", out _, out _), Is.False,
                "Evidence: rebuilding an unchanged active grid deletes the snapshot.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DataDisconnectionLeavesOpticalCameraActive()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid scanner = default;
        await pair.Server.WaitAssertion(() =>
        {
            for (var x = 0; x < 8; x++) em.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
            EntityUid Spawn(string id)
            {
                var uid = em.SpawnEntity(id, new EntityCoordinates(map.Grid, .5f, .5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid); return uid;
            }
            Spawn("KiasCore"); Spawn("KiasDataCable"); scanner = Spawn("KiasRoomScanner");
            em.System<KiasSystem>().Rebuild(map.Grid);
        });
        await pair.RunTicksSync(10);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<SurveillanceCameraComponent>(scanner).Active, Is.True);
            var transform = em.System<SharedTransformSystem>();
            transform.Unanchor(scanner); transform.SetCoordinates(scanner, new EntityCoordinates(map.Grid, 7.5f, .5f)); transform.AnchorEntity(scanner);
            em.System<KiasSystem>().Rebuild(map.Grid);
            Assert.That(em.System<KiasSystem>().IsOnline(scanner), Is.False);
            Assert.That(em.GetComponent<SurveillanceCameraComponent>(scanner).Active, Is.True,
                "Evidence: the offline scanner still exposes an active native camera.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirtyFirstGridBlocksSecondGridEvent()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var a = await pair.CreateTestMap(); var b = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid coreA = default, coreB = default, cardB = default;
        await pair.Server.WaitAssertion(() =>
        {
            (EntityUid Core, EntityUid Card) Setup(EntityUid grid)
            {
                EntityUid Spawn(string id)
                {
                    var uid = em.SpawnEntity(id, new EntityCoordinates(grid, .5f, .5f));
                    em.RemoveComponent<ApcPowerReceiverComponent>(uid); return uid;
                }
                var core = Spawn("KiasCore"); Spawn("KiasDataCable"); var rack = Spawn("KiasControllerRack");
                var card = Spawn("KiasProgrammableController"); var program = new KiasControllerProgram();
                program.Nodes.Add(new() { Id = 1, Kind = KiasNodeKind.Any, Profile = "Automation" });
                program.Nodes.Add(new() { Id = 2, Kind = KiasNodeKind.Counter });
                program.Wires.Add(new() { FromNode = 1, FromPort = "Manual", ToNode = 2, ToPort = "Increment" });
                em.GetComponent<KiasControllerCardComponent>(card).Program = program;
                em.System<KiasSystem>().Rebuild(grid);
                Assert.That(em.System<ItemSlotsSystem>().TryInsert(rack, KiasControllerRackComponent.SlotId(0), card, null), Is.True);
                return (core, card);
            }
            (coreA, _) = Setup(a.Grid); (coreB, cardB) = Setup(b.Grid);
        });
        await pair.RunTicksSync(15);
        await pair.Server.WaitAssertion(() =>
        {
            var runtime = em.System<KiasControllerRuntimeSystem>(); var io = em.System<KiasControllerIoSystem>();
            Assert.That(runtime.Running(cardB), Is.True);
            io.Emit(coreA, "Automation", "Manual", KiasGraphValue.Pulse);
            io.Emit(coreB, "Automation", "Manual", KiasGraphValue.Pulse);
            em.System<KiasSystem>().Invalidate(a.Grid);
            runtime.Update(0);
            Assert.That(runtime.LastValue(cardB, 2, "Value").Number, Is.Zero,
                "Evidence: a ready unrelated ship cannot consume its queued event.");
            em.System<KiasSystem>().Rebuild(a.Grid); runtime.Update(0);
            Assert.That(runtime.LastValue(cardB, 2, "Value").Number, Is.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestPulseCanActuateAfterFiveSecondTestExpires()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid scanner = default, adapter = default, tool = default, core = default, card = default;
        await pair.Server.WaitAssertion(() =>
        {
            EntityUid Spawn(string id)
            {
                var uid = em.SpawnEntity(id, new EntityCoordinates(map.Grid, .5f, .5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid); return uid;
            }
            core = Spawn("KiasCore"); Spawn("KiasDataCable"); scanner = Spawn("KiasRoomScanner");
            adapter = Spawn("KiasDeviceAdapter"); var rack = Spawn("KiasControllerRack"); card = Spawn("KiasProgrammableController");
            tool = Spawn("KiasServiceTool"); em.GetComponent<KiasServiceToolComponent>(tool).Mode = KiasServiceMode.Test;
            em.System<KiasSystem>().Rebuild(map.Grid);
            var io = em.System<KiasControllerIoSystem>();
            var profile = io.Profiles(scanner).Single(p => p.StartsWith("Link."));
            var program = new KiasControllerProgram();
            program.Nodes.Add(new() { Id = 1, Kind = KiasNodeKind.Specific, Profile = profile, Binding = scanner });
            program.Nodes.Add(new() { Id = 2, Kind = KiasNodeKind.Timer, Config = new() { Seconds = 6 } });
            program.Nodes.Add(new() { Id = 3, Kind = KiasNodeKind.Specific, Profile = "DeviceAdapter", Binding = adapter });
            program.Wires.Add(new() { FromNode = 1, FromPort = "out:KiasMotion", ToNode = 2, ToPort = "Trigger" });
            program.Wires.Add(new() { FromNode = 2, FromPort = "Elapsed", ToNode = 3, ToPort = "On" });
            em.GetComponent<KiasControllerCardComponent>(card).Program = program;
            Assert.That(em.System<ItemSlotsSystem>().TryInsert(rack, KiasControllerRackComponent.SlotId(0), card, null), Is.True);
        });
        await pair.RunTicksSync(15);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.System<KiasControllerRuntimeSystem>().Running(card), Is.True, em.System<KiasControllerRuntimeSystem>().Fault(card));
            var use = new AfterInteractEvent(core, tool, scanner, new EntityCoordinates(map.Grid, .5f, .5f), true);
            em.EventBus.RaiseLocalEvent(tool, use);
            Assert.That(em.GetComponent<KiasGridComponent>(map.Grid).Testing, Is.True);
        });
        await pair.RunTicksSync(430);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<KiasGridComponent>(map.Grid).Testing, Is.False);
            Assert.That(em.GetComponent<KiasDeviceAdapterComponent>(adapter).State, Is.True,
                "Evidence: a diagnostic pulse caused a real delayed action.");
        });
        await pair.CleanReturnAsync();
    }
}
