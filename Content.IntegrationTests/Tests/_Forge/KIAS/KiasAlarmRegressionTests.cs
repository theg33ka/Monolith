using System.Linq;
using System.Collections.Generic;
using Content.Server._Forge.KIAS;
using Content.Server._Forge.KIAS.Controllers;
using Content.Server.Weapons.Ranged.Systems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.Power;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Content.Shared._Forge.KIAS.Controllers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasAlarmRegressionTests
{
    private static EntityUid Spawn(IEntityManager em, EntityUid grid, string prototype, int x)
    {
        var uid = em.SpawnEntity(prototype, new EntityCoordinates(grid, x + .5f, .5f));
        em.RemoveComponent<ApcPowerReceiverComponent>(uid);
        return uid;
    }

    private static EntityUid Setup(IEntityManager em, EntityUid grid, Tile tile)
    {
        var map = em.GetComponent<MapGridComponent>(grid);
        map.CanSplit = false;
        for (var x = 0; x < 20; x++)
        {
            em.System<SharedMapSystem>().SetTile(grid, map, new Vector2i(x, 0), tile);
            if (x < 6) Spawn(em, grid, "KiasDataCable", x);
        }
        var core = Spawn(em, grid, "KiasCore", 0);
        Spawn(em, grid, "KiasDefenceServer", 1);
        Spawn(em, grid, "KiasIntegrityMonitor", 2);
        Spawn(em, grid, "KiasNavigationServer", 3);
        Spawn(em, grid, "KiasMaydayAntenna", 4);
        Spawn(em, grid, "KiasRecorder", 5);
        em.System<KiasSystem>().Rebuild(grid);
        return core;
    }

    [Test]
    public async Task DamageNotificationsShareOneBudgetAcrossDifferentStructures()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        var io = em.System<KiasControllerIoSystem>();
        var emitted = 0;
        var announced = 0;
        var walls = new List<EntityUid>();
        void Count(EntityUid device, string profile, string port, Content.Shared._Forge.KIAS.Controllers.KiasGraphValue value)
        {
            if (profile == "Automation" && port == "HullDamage") emitted++;
        }
        void Command(EntityUid grid, EntityUid card, EntityUid target, string profile, string port)
        {
            if (profile == "Speaker" && port == "Announce") announced++;
        }
        await pair.Server.WaitAssertion(() =>
        {
            Setup(em, map.Grid, map.Tile.Tile);
            for (var x = 0; x < 20; x++)
                walls.Add(Spawn(em, map.Grid, "WallSolid", x));
            Spawn(em, map.Grid, "KiasSpeaker", 5);
            var rack = Spawn(em, map.Grid, "KiasControllerRack", 0);
            var card = Spawn(em, map.Grid, "KiasProgrammableController", 0);
            var program = new KiasControllerProgram { Name = "Unfiltered hull notifications" };
            program.Nodes.Add(new() { Id = 1, Kind = KiasNodeKind.Any, Profile = "Automation" });
            program.Nodes.Add(new() { Id = 2, Kind = KiasNodeKind.All, Profile = "Speaker" });
            program.Wires.Add(new() { FromNode = 1, FromPort = "HullDamage", ToNode = 2, ToPort = "Announce" });
            program.Wires.Add(new() { FromNode = 1, FromPort = "EventKey", ToNode = 2, ToPort = "Key" });
            program.Wires.Add(new() { FromNode = 1, FromPort = "Message", ToNode = 2, ToPort = "Message" });
            em.GetComponent<KiasControllerCardComponent>(card).Program = program;
            em.System<KiasSystem>().Rebuild(map.Grid);
            Assert.That(em.System<ItemSlotsSystem>().TryInsert(rack, KiasControllerRackComponent.SlotId(0), card, null), Is.True);
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.System<KiasHullSystem>().HasMonitor<KiasIntegrityMonitorComponent>(map.Grid), Is.True);
            io.Emitted += Count;
            foreach (var wall in walls)
            {
                var damage = new KiasHullDamageEvent(map.Grid, wall, 20);
                em.EventBus.RaiseLocalEvent(map.Grid, ref damage, true);
            }
            Assert.That(emitted, Is.EqualTo(20));
            io.Emitted -= Count;
            io.CommandDispatched += Command;
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(announced, Is.EqualTo(1));
            io.CommandDispatched -= Command;
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(50)]
    [TestCase(500)]
    [TestCase(5000)]
    public async Task NativeHullHitsAreBatchedBeforeGraphEvents(int hits)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        var io = em.System<KiasControllerIoSystem>();
        var damageEvents = 0;
        var totalDamage = 0d;
        EntityUid core = default, wall = default;
        void Count(EntityUid device, string profile, string port, KiasGraphValue value)
        {
            if (profile != "IntegrityMonitor" || port != "Damage") return;
            damageEvents++;
            totalDamage += io.TryOutput(device, profile, "Amount", out var amount, out _) ? amount.Number : 0;
        }

        await pair.Server.WaitAssertion(() =>
        {
            core = Setup(em, map.Grid, map.Tile.Tile);
            wall = Spawn(em, map.Grid, "WallSolid", 8);
            io.Emitted += Count;
            var damageable = em.GetComponent<DamageableComponent>(wall);
            var delta = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(1) } };
            for (var i = 0; i < hits; i++)
                em.EventBus.RaiseLocalEvent(wall, new DamageChangedEvent(damageable, delta, false, null));

            Assert.That(damageEvents, Is.Zero, "Native hit callbacks must not fan out into KIAS graph events immediately.");
            Assert.That(em.GetComponent<KiasProtocolComponent>(core).RecentDamage, Is.Zero);
        });

        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(damageEvents, Is.EqualTo(1));
            Assert.That(totalDamage, Is.EqualTo(hits).Within(0.01));
            Assert.That(em.GetComponent<KiasProtocolComponent>(core).RecentDamage, Is.EqualTo(hits).Within(0.01));
            io.Emitted -= Count;
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("WeaponPistolMk58", false)]
    [TestCase("WeaponTurretSyndicate", false)]
    [TestCase("WeaponTurretAK570", true)]
    public async Task WeaponFlashDetectorOnlyAcceptsRadarMarkedShipWeapons(string prototype, bool detected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        var maps = em.System<SharedMapSystem>();
        var transforms = em.System<SharedTransformSystem>();
        var io = em.System<KiasControllerIoSystem>();
        EntityUid detector = default, foreignGrid = default, gun = default;
        var triggers = 0;
        void Count(EntityUid device, string profile, string port, KiasGraphValue value)
        {
            if (device == detector && profile == "WeaponFlashDetector" && port == "Triggered") triggers++;
        }

        await pair.Server.WaitAssertion(() =>
        {
            Setup(em, map.Grid, map.Tile.Tile);
            detector = Spawn(em, map.Grid, "KiasWeaponFlashDetector", 5);
            foreignGrid = maps.CreateGridEntity(map.MapId).Owner;
            maps.SetTile(foreignGrid, em.GetComponent<MapGridComponent>(foreignGrid), Vector2i.Zero, map.Tile.Tile);
            em.EnsureComponent<ShuttleComponent>(foreignGrid);
            em.EnsureComponent<IFFComponent>(foreignGrid);
            gun = em.SpawnEntity(prototype, new EntityCoordinates(foreignGrid, .5f, .5f));
            em.System<KiasSystem>().Rebuild(map.Grid);
            var direction = transforms.GetWorldRotation(detector).ToWorldVec();
            var source = transforms.GetMapCoordinates(detector).Position + direction * 100;
            var gunOffset = transforms.GetMapCoordinates(gun).Position - transforms.GetWorldPosition(foreignGrid);
            transforms.SetWorldPosition(foreignGrid, source - gunOffset);
            io.Emitted += Count;
        });

        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.System<KiasSystem>().IsOnline(detector), Is.True);
            Assert.That(em.System<KiasSystem>().HasRole(map.Grid, KiasDeviceRole.Defence), Is.True);
            Assert.That(io.Profiles(detector), Does.Contain("WeaponFlashDetector"));
            Assert.That(em.HasComponent<GunComponent>(gun), Is.True);
            var cartridge = em.SpawnEntity("Cartridge45_ACPFMJ", new EntityCoordinates(foreignGrid, .5f, .5f));
            var direction = transforms.GetWorldRotation(detector).ToWorldVec();
            var target = transforms.GetMapCoordinates(gun).Position + direction * 200;
            em.System<GunSystem>().Shoot(gun, em.GetComponent<GunComponent>(gun),
                new List<(EntityUid? Entity, IShootable Shootable)> { (cartridge, em.GetComponent<CartridgeAmmoComponent>(cartridge)) },
                new EntityCoordinates(foreignGrid, .5f, .5f), new EntityCoordinates(maps.GetMap(map.MapId), target), out _);
            Assert.That(triggers, Is.EqualTo(detected ? 1 : 0), "Only radar-marked ship artillery may trigger the flash detector.");
            io.Emitted -= Count;
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MinorHullLossDoesNotBecomeCriticalAndMajorLossRequiresConfirmation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        var engine = em.System<KiasProtocolSystem>();
        EntityUid core = default;
        await pair.Server.WaitAssertion(() =>
        {
            core = Setup(em, map.Grid, map.Tile.Tile);
        });
        await pair.RunTicksSync(8);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.System<KiasHullSystem>().HasMonitor<KiasIntegrityMonitorComponent>(map.Grid), Is.True);
            var damage = new KiasHullDamageEvent(map.Grid, core, 400);
            em.EventBus.RaiseLocalEvent(map.Grid, ref damage, true);
            var protocols = em.GetComponent<KiasProtocolComponent>(core);
            Assert.That(protocols.DamageEvidenceUntil, Is.GreaterThan(pair.Server.ResolveDependency<IGameTiming>().CurTime));
            em.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(19, 0), Tile.Empty);
        });
        await pair.RunTicksSync(70);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(engine.HasCriticalHullFailure(map.Grid), Is.False);
            Assert.That(em.GetComponent<KiasProtocolComponent>(core).CriticalLatched, Is.False);
            for (var x = 8; x < 19; x++)
                em.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), Tile.Empty);
        });
        await pair.RunTicksSync(70);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(engine.HasCriticalHullFailure(map.Grid), Is.True);
            var protocols = em.GetComponent<KiasProtocolComponent>(core);
            Assert.That(protocols.CriticalLatched, Is.False);
            protocols.CriticalSince = pair.Server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(16);
        });
        await pair.RunTicksSync(70);
        await pair.Server.WaitAssertion(() => Assert.That(em.GetComponent<KiasProtocolComponent>(core).CriticalLatched, Is.True));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OwnFragmentsAndUnidentifiedDebrisAreNotVesselContacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var fragmentMap = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var sensors = em.System<KiasExternalSensorSystem>();
            em.EnsureComponent<ShuttleComponent>(fragmentMap.Grid);
            em.EnsureComponent<IFFComponent>(fragmentMap.Grid);
            em.EnsureComponent<KiasGridOriginComponent>(map.Grid);
            em.EnsureComponent<KiasGridOriginComponent>(fragmentMap.Grid);
            Assert.That(sensors.IsVesselContact(map.Grid, fragmentMap.Grid), Is.True);
            var split = new GridSplitEvent(new[] { fragmentMap.Grid.Owner }, map.Grid);
            em.EventBus.RaiseLocalEvent(map.Grid, ref split, true);
            Assert.That(sensors.IsVesselContact(map.Grid, fragmentMap.Grid), Is.False);
            em.RemoveComponent<KiasGridOriginComponent>(fragmentMap.Grid);
            em.RemoveComponent<IFFComponent>(fragmentMap.Grid);
            Assert.That(sensors.IsVesselContact(map.Grid, fragmentMap.Grid), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task MaydayWaitsForCrewResponseAndAcknowledgementOrRecoveryCancelsIt(bool acknowledge)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        var engine = em.System<KiasProtocolSystem>();
        EntityUid core = default;
        await pair.Server.WaitAssertion(() =>
        {
            core = Setup(em, map.Grid, map.Tile.Tile);
            Assert.That(engine.Mayday(map.Grid, "No emergency"), Is.False);
            var state = em.GetComponent<KiasProtocolComponent>(core);
            var now = pair.Server.ResolveDependency<IGameTiming>().CurTime;
            state.DamageEvidenceUntil = now + TimeSpan.FromSeconds(60);
            Assert.That(engine.Mayday(map.Grid, "Hull failure"), Is.True);
            Assert.That(state.CrewResponseDeadline, Is.GreaterThanOrEqualTo(now + TimeSpan.FromSeconds(30)));
            Assert.That(em.HasComponent<KiasMaydayComponent>(map.Grid), Is.False);
            Assert.That(em.GetComponent<KiasGridComponent>(map.Grid).Log.Any(line => line.Contains("MAYDAY")), Is.True);
            if (acknowledge)
            {
                Assert.That(engine.AcknowledgeCrew(map.Grid), Is.True);
                Assert.That(state.PendingMaydayReason, Is.Empty);
                Assert.That(engine.Mayday(map.Grid, "Still damaged"), Is.False);
            }
            else state.DamageEvidenceUntil = TimeSpan.Zero;
        });
        await pair.RunTicksSync(80);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.HasComponent<KiasMaydayComponent>(map.Grid), Is.False);
            Assert.That(em.GetComponent<KiasProtocolComponent>(core).PendingMaydayReason, Is.Empty);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IntegratedDevicePowerLossStaysInDiagnostics()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            Setup(em, map.Grid, map.Tile.Tile);
            var alarm = Spawn(em, map.Grid, "AirAlarm", 1);
            em.EnsureComponent<KiasIntegratedComponent>(alarm);
            em.EnsureComponent<KiasDeviceComponent>(alarm);
            em.System<KiasSystem>().Rebuild(map.Grid);
            var count = em.GetComponent<KiasGridComponent>(map.Grid).Log.Count;
            var lost = new PowerChangedEvent(false, 0);
            em.EventBus.RaiseLocalEvent(alarm, ref lost);
            Assert.That(em.GetComponent<KiasGridComponent>(map.Grid).Log.Count, Is.EqualTo(count));
        });
        await pair.CleanReturnAsync();
    }
}
