using Content.Server._Forge.KIAS;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasProtocolTests
{
    [Test]
    public async Task MedicalAssistanceRequiresDistressAndUnavailableRegisteredCrew()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            for (var x = 0; x < 5; x++)
            {
                em.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
                em.SpawnEntity("KiasDataCable", new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
            }
            EntityUid Spawn(string prototype, int x)
            {
                var uid = em.SpawnEntity(prototype, new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid);
                return uid;
            }
            var core = Spawn("KiasCore", 0);
            var crewServer = Spawn("KiasCrewServer", 1);
            var scanner = Spawn("KiasRoomScanner", 2);
            Spawn("KiasRecorder", 3);
            var containers = em.System<SharedContainerSystem>();
            var bio = em.SpawnEntity("KiasBiometricModule", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            containers.Insert(bio, containers.GetContainer(scanner, "kias-module-3"));
            var body = em.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            em.AddComponent<KiasTrackedEntityComponent>(body);
            var token = em.SpawnEntity("KiasCrewTransponder", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            var transponder = em.GetComponent<KiasTransponderComponent>(token);
            transponder.Core = core;
            em.GetComponent<KiasCrewServerComponent>(crewServer).Registered.Add(transponder.Serial);
            containers.Insert(token, containers.EnsureContainer<Container>(body, "kias-test-token"));
            em.System<KiasSystem>().Rebuild(map.Grid);
            var record = new KiasProtocolRecord { Trigger = KiasTrigger.VesselCritical, RequireCrewUnavailable = true };
            record.Actions.Add(new KiasProtocolAction { Kind = KiasActionKind.Record, Message = "Medical assistance" });
            var config = em.GetComponent<KiasProtocolComponent>(core);
            config.Protocols.Add(record);
            var crew = em.System<KiasCrewSystem>();
            var engine = em.System<KiasProtocolSystem>();
            var runtime = em.GetComponent<KiasGridComponent>(map.Grid);
            Assert.That(crew.CrewUnavailable(map.Grid), Is.False);
            engine.Trigger(map.Grid, KiasTrigger.VesselCritical);
            Assert.That(runtime.Log, Is.Empty);
            em.System<MobStateSystem>().ChangeMobState(body, MobState.Critical);
            Assert.That(crew.CrewUnavailable(map.Grid), Is.True);
            var before = runtime.Log.Count;
            engine.Trigger(map.Grid, KiasTrigger.VesselCritical);
            Assert.That(runtime.Log.Count, Is.EqualTo(before + 1));
            config.Cooldowns.Clear();
            record.Trigger = KiasTrigger.Manual;
            engine.Trigger(map.Grid, KiasTrigger.Manual);
            Assert.That(runtime.Log.Count, Is.EqualTo(before + 1), "An empty/inactive crew alone must not trigger medical assistance.");
            em.GetComponent<KiasCrewServerComponent>(crewServer).Registered.Clear();
            Assert.That(crew.CrewUnavailable(map.Grid), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CooldownTestModeForeignTargetMaydayAndHardOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var maps = em.System<SharedMapSystem>();
            for (var x = 0; x < 6; x++)
            {
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
                em.SpawnEntity("KiasDataCable", new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
            }
            EntityUid Spawn(string prototype, int x)
            {
                var uid = em.SpawnEntity(prototype, new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid);
                return uid;
            }
            var core = Spawn("KiasCore", 0);
            Spawn("KiasNavigationServer", 1);
            Spawn("KiasRecorder", 2);
            Spawn("KiasMaydayAntenna", 3);
            var relay = Spawn("KiasRelay", 4);
            em.EnsureComponent<IFFComponent>(map.Grid);
            var config = em.GetComponent<KiasProtocolComponent>(core);
            var record = new KiasProtocolRecord { Trigger = KiasTrigger.Manual, Cooldown = 10 };
            record.Actions.Add(new KiasProtocolAction { Kind = KiasActionKind.Record, Message = "Once" });
            record.Actions.Add(new KiasProtocolAction { Kind = KiasActionKind.Relay, Target = relay, Value = false });
            config.Protocols.Add(record);
            var kias = em.System<KiasSystem>();
            kias.Rebuild(map.Grid);
            var runtime = em.GetComponent<KiasGridComponent>(map.Grid);
            runtime.Testing = true;
            var engine = em.System<KiasProtocolSystem>();
            engine.Trigger(map.Grid, KiasTrigger.Manual);
            engine.Trigger(map.Grid, KiasTrigger.Manual);
            Assert.That(runtime.Log.Count, Is.EqualTo(1));
            Assert.That(em.GetComponent<KiasRelayComponent>(relay).Closed, Is.True);
            runtime.Testing = false;
            config.Cooldowns.Clear();
            engine.Trigger(map.Grid, KiasTrigger.Manual);
            Assert.That(em.GetComponent<KiasRelayComponent>(relay).Closed, Is.False);
            Assert.That(runtime.Log.Count, Is.EqualTo(1));
            kias.Rebuild(map.Grid);
            Assert.That(engine.Mayday(map.Grid, "Test distress"), Is.True);
            Assert.That(engine.Mayday(map.Grid, "Repeat"), Is.False);
            Assert.That(em.System<SharedShuttleSystem>().GetIFFLabel(map.Grid), Does.Contain("[MAYDAY]"));
            kias.SetEnabled(core, false);
            var count = runtime.Log.Count;
            engine.Trigger(map.Grid, KiasTrigger.Manual);
            Assert.That(runtime.Log.Count, Is.EqualTo(count));
            Assert.That(em.HasComponent<KiasMaydayComponent>(map.Grid), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
