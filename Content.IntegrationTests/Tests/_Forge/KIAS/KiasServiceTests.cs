using System.Linq;
using Content.Client._Forge.KIAS;
using Content.Server._Forge.KIAS;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Power;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasServiceTests
{
    [Test]
    public async Task NativeGridSplitSeparatesDeviceRegistries()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid core = default;
        EntityUid server = default;
        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.ResolveDependency<Robust.Shared.Configuration.IConfigurationManager>().SetCVar(Robust.Shared.CVars.GridSplitting, true);
            map.Grid.Comp.CanSplit = true;
            for (var x = 0; x < 7; x++)
            {
                em.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
                if (x != 3)
                    em.SpawnEntity("KiasDataCable", new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
            }
            core = em.SpawnEntity("KiasCore", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            server = em.SpawnEntity("KiasCrewServer", new EntityCoordinates(map.Grid, 6.5f, 0.5f));
            em.RemoveComponent<ApcPowerReceiverComponent>(core);
            em.RemoveComponent<ApcPowerReceiverComponent>(server);
            em.System<KiasSystem>().Rebuild(map.Grid);
            em.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(3, 0), Tile.Empty);
            em.System<Robust.Server.Physics.GridFixtureSystem>().CheckSplits(map.Grid);
        });
        await pair.RunTicksSync(3);
        await pair.Server.WaitAssertion(() =>
        {
            var coreGrid = em.GetComponent<TransformComponent>(core).GridUid!.Value;
            var otherGrid = em.GetComponent<TransformComponent>(server).GridUid!.Value;
            Assert.That(coreGrid, Is.Not.EqualTo(otherGrid));
            Assert.That(em.GetComponent<KiasGridComponent>(coreGrid).Devices, Does.Not.Contain(server));
            Assert.That(em.GetComponent<KiasGridComponent>(otherGrid).Devices, Does.Contain(server));
            Assert.That(em.System<KiasSystem>().ActiveGrids, Does.Contain(coreGrid));
            Assert.That(em.System<KiasSystem>().IsOnline(server), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OwnerConfigurationRejectsForeignTargetsAndClientWindowBuilds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var other = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var session = pair.Server.ResolveDependency<IPlayerManager>().Sessions.Single();
            var actor = em.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var stranger = em.SpawnEntity("MobCat", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var mind = em.System<SharedMindSystem>().CreateMind(session.UserId);
            em.System<SharedMindSystem>().TransferTo(mind, actor);
            em.EnsureComponent<ShipOwnershipComponent>(map.Grid).OwnerUserId = session.UserId;
            EntityUid Spawn(string prototype, EntityUid grid, int x)
            {
                em.System<SharedMapSystem>().SetTile(grid, em.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(grid), new Vector2i(x, 0), map.Tile.Tile);
                em.SpawnEntity("KiasDataCable", new EntityCoordinates(grid, x + 0.5f, 0.5f));
                var uid = em.SpawnEntity(prototype, new EntityCoordinates(grid, x + 0.5f, 0.5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid);
                return uid;
            }
            var core = Spawn("KiasCore", map.Grid, 0);
            var display = Spawn("KiasDisplay", map.Grid, 1);
            var defence = Spawn("KiasDefenceServer", map.Grid, 2);
            var foreign = Spawn("KiasRelay", other.Grid, 0);
            em.System<KiasSystem>().Rebuild(map.Grid);
            var protocols = em.GetComponent<KiasProtocolComponent>(core);
            protocols.Protocols.Clear();
            var edit = new KiasProtocolMessage { Actor = stranger, Index = 0, Trigger = KiasTrigger.Manual, Action = KiasActionKind.Record, Message = "Owner only" };
            em.EventBus.RaiseLocalEvent(display, edit);
            Assert.That(protocols.Protocols, Is.Empty);
            edit.Actor = actor;
            edit.Target = em.GetNetEntity(foreign);
            em.EventBus.RaiseLocalEvent(display, edit);
            Assert.That(protocols.Protocols, Is.Empty);
            edit.Target = null;
            em.EventBus.RaiseLocalEvent(display, edit);
            Assert.That(protocols.Protocols, Has.Count.EqualTo(1));
            Assert.That(em.System<KiasDefenceSystem>().SetEnabled(defence, stranger, true), Is.False);
            Assert.That(em.System<KiasDefenceSystem>().SetEnabled(defence, actor, true), Is.True);
            protocols.Alert = KiasAlert.Emergency;
            var reset = new KiasControlMessage { Actor = stranger, Reset = true };
            em.EventBus.RaiseLocalEvent(display, reset);
            Assert.That(protocols.Alert, Is.EqualTo(KiasAlert.Emergency));
            reset.Actor = actor;
            em.EventBus.RaiseLocalEvent(display, reset);
            Assert.That(protocols.Alert, Is.EqualTo(KiasAlert.Normal));
        });
        await pair.Client.WaitAssertion(() =>
        {
            using var window = new KiasWindow();
            var state = new KiasUiState { Online = true, ServiceTool = true, ProtocolsAvailable = true, Entities = 4, Crew = 2 };
            for (var i = 0; i < 81; i++)
                state.Coverage.Add((byte) (i == 40 ? 7 : 1));
            state.Protocols.Add(new KiasProtocolView { Trigger = KiasTrigger.Fire, Action = KiasActionKind.Suppression, Cooldown = 10, Enabled = true });
            Assert.DoesNotThrow(() => window.UpdateState(state));
            state.ProtocolRevision++;
            Assert.DoesNotThrow(() => window.UpdateState(state));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestExpiresAndMovementPowerLossInvalidateRuntime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid core = default;
        EntityUid scanner = default;
        EntityUid tool = default;
        await pair.Server.WaitAssertion(() =>
        {
            var maps = em.System<SharedMapSystem>();
            for (var x = 0; x < 8; x++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
            em.SpawnEntity("KiasDataCable", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            core = em.SpawnEntity("KiasCore", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            scanner = em.SpawnEntity("KiasRoomScanner", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            em.RemoveComponent<ApcPowerReceiverComponent>(core);
            em.RemoveComponent<ApcPowerReceiverComponent>(scanner);
            tool = em.SpawnEntity("KiasServiceTool", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            em.GetComponent<KiasServiceToolComponent>(tool).Mode = KiasServiceMode.Test;
            em.System<KiasSystem>().Rebuild(map.Grid);
            var use = new AfterInteractEvent(core, tool, scanner, new EntityCoordinates(map.Grid, 1.5f, 0.5f), true);
            em.EventBus.RaiseLocalEvent(tool, use);
            Assert.That(em.GetComponent<KiasGridComponent>(map.Grid).Testing, Is.True);
        });
        await pair.RunTicksSync(310);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<KiasGridComponent>(map.Grid).Testing, Is.False);
            var transform = em.System<SharedTransformSystem>();
            transform.Unanchor(scanner);
            transform.SetCoordinates(scanner, new EntityCoordinates(map.Grid, 6.5f, 0.5f));
            transform.AnchorEntity(scanner);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<KiasDeviceComponent>(scanner).Status, Is.EqualTo(KiasDeviceStatus.NoDataPath));
            em.AddComponent<ApcPowerReceiverComponent>(core);
            var lost = new PowerChangedEvent(false, 0);
            em.EventBus.RaiseLocalEvent(core, ref lost);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.System<KiasSystem>().ActiveGrids, Does.Not.Contain(map.Grid.Owner));
            Assert.That(em.GetComponent<KiasDeviceComponent>(core).Status, Is.EqualTo(KiasDeviceStatus.NoPower));
        });
        await pair.CleanReturnAsync();
    }
}
