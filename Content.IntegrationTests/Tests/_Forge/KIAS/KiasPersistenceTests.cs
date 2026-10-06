using System.Linq;
using Content.Server._Forge.KIAS;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.Power;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasPersistenceTests
{
    [Test]
    public async Task RelayTokenProtocolAndRecorderSurviveMapRoundTrip()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var em = server.ResolveDependency<IEntityManager>();
        var maps = em.System<SharedMapSystem>();
        var loader = em.System<MapLoaderSystem>();
        var path = new ResPath("/Maps/Test/KiasRoundTrip.yml");
        var serial = Guid.NewGuid().ToString("N");
        await server.WaitAssertion(() =>
        {
            for (var x = 0; x < 9; x++)
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
            var recorder = Spawn("KiasRecorder", 1);
            var crew = Spawn("KiasCrewServer", 2);
            var relay = Spawn("KiasRelay", 4);
            var speaker = Spawn("KiasSpeaker", 6);
            var scanner = Spawn("KiasRoomScanner", 7);
            em.GetComponent<KiasSpeakerComponent>(speaker).Links.Add(new KiasSpeakerLink { Source = scanner, SourcePort = "KiasMotion", Message = "Saved custom message" });
            var token = em.SpawnEntity("KiasCrewTransponder", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            var tokenComponent = em.GetComponent<KiasTransponderComponent>(token);
            tokenComponent.Serial = serial;
            tokenComponent.Core = core;
            em.GetComponent<KiasCrewServerComponent>(crew).Registered.Add(serial);
            em.GetComponent<KiasRecorderComponent>(recorder).Entries.Add("Saved event");
            var protocol = new KiasProtocolRecord { Trigger = KiasTrigger.HullImpact, Cooldown = 30 };
            protocol.Actions.Add(new KiasProtocolAction { Kind = KiasActionKind.Relay, Target = relay, Value = false });
            em.GetComponent<KiasProtocolComponent>(core).Protocols.Clear();
            em.GetComponent<KiasProtocolComponent>(core).Protocols.Add(protocol);
            em.System<KiasSystem>().Rebuild(map.Grid);
            Assert.That(em.System<KiasRelaySystem>().SetClosed(relay, false), Is.True);
            server.ResolveDependency<IResourceManager>().UserData.CreateDir(path.Directory);
            Assert.That(loader.TrySaveMap(map.MapId, path), Is.True);
            maps.DeleteMap(map.MapId);
        });
        await server.WaitIdleAsync();
        EntityUid loadedGrid = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(path, out _, out var grids), Is.True);
            loadedGrid = grids!.Single();
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var candidates = new System.Collections.Generic.HashSet<EntityUid>();
            var grid = em.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(loadedGrid);
            em.System<EntityLookupSystem>().GetLocalEntitiesIntersecting(loadedGrid, grid.LocalAABB, candidates);
            var core = candidates.Single(em.HasComponent<KiasCoreComponent>);
            var relay = candidates.Single(em.HasComponent<KiasRelayComponent>);
            var recorder = candidates.Single(em.HasComponent<KiasRecorderComponent>);
            var crew = candidates.Single(em.HasComponent<KiasCrewServerComponent>);
            var speaker = candidates.Single(em.HasComponent<KiasSpeakerComponent>);
            var scanner = candidates.Single(em.HasComponent<KiasRoomScannerComponent>);
            var tokens = em.EntityQueryEnumerator<KiasTransponderComponent>();
            Assert.That(tokens.MoveNext(out _, out var token), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(em.GetComponent<KiasRelayComponent>(relay).Closed, Is.False);
                Assert.That(em.GetComponent<KiasRelayComponent>(relay).Channel, Is.EqualTo(CableType.Data));
                Assert.That(token.Serial, Is.EqualTo(serial));
                Assert.That(token.Core, Is.EqualTo(core));
                Assert.That(em.GetComponent<KiasCrewServerComponent>(crew).Registered, Does.Contain(serial));
                Assert.That(em.GetComponent<KiasRecorderComponent>(recorder).Entries, Does.Contain("Saved event"));
                Assert.That(em.GetComponent<KiasProtocolComponent>(core).Protocols.Single().Actions.Single().Target, Is.EqualTo(relay));
                Assert.That(em.GetComponent<KiasSpeakerComponent>(speaker).Links.Single().Source, Is.EqualTo(scanner));
                Assert.That(em.GetComponent<KiasSpeakerComponent>(speaker).Links.Single().Message, Is.EqualTo("Saved custom message"));
            });
            em.System<KiasSystem>().Rebuild(loadedGrid);
            Assert.That(em.System<KiasRelaySystem>().IsBlocked(loadedGrid, new Vector2i(4, 0), CableType.Data), Is.True);
            maps.DeleteMap(em.GetComponent<TransformComponent>(loadedGrid).MapID);
        });
        await pair.CleanReturnAsync();
    }
}
