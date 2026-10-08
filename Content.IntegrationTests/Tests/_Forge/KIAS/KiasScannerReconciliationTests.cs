using Content.Server._Forge.KIAS;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasScannerReconciliationTests
{
    [Test]
    public async Task ShrinkingCoverageHandsOffAndClearsOnlyAutomaticBindings()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        EntityUid first = default, second = default, alarm = default, direct = default;
        await pair.Server.WaitAssertion(() =>
        {
            for (var x = 0; x < 9; x++)
                em.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
            EntityUid Spawn(string id, float x = .5f)
            {
                var uid = em.SpawnEntity(id, new EntityCoordinates(map.Grid, x, .5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid);
                return uid;
            }
            Spawn("KiasDataCable"); Spawn("KiasCore"); Spawn("KiasAtmosServer"); Spawn("KiasCrewServer");
            first = Spawn("KiasRoomScanner"); second = Spawn("KiasRoomScanner");
            foreach (var scanner in new[] { first, second })
            {
                em.GetComponent<KiasRoomScannerComponent>(scanner).Range = 10;
                Assert.That(em.System<ItemSlotsSystem>().TryInsert(scanner, "kias-module-3", Spawn("KiasConnectorModule"), null), Is.True);
            }
            alarm = Spawn("AirAlarm", 7.5f);
            direct = Spawn("AirAlarm", 8.5f);
            em.EnsureComponent<KiasIntegratedComponent>(direct).Direct = true;
            em.EnsureComponent<KiasDeviceComponent>(direct).Role = KiasDeviceRole.Adapter;
        });
        await pair.RunTicksSync(60);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<KiasIntegratedComponent>(alarm).Scanner, Is.EqualTo(first));
            em.GetComponent<KiasRoomScannerComponent>(first).Range = 2;
            em.System<KiasCrewSystem>().RebuildCoverage(map.Grid);
            Assert.That(em.GetComponent<KiasIntegratedComponent>(alarm).Scanner, Is.EqualTo(second));
        });
        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() =>
        {
            em.GetComponent<KiasRoomScannerComponent>(second).Range = 2;
            em.System<KiasCrewSystem>().RebuildCoverage(map.Grid);
            Assert.That(em.GetComponent<KiasIntegratedComponent>(alarm).Scanner, Is.Null);
            Assert.That(em.System<KiasIntegrationSystem>().CanControl(alarm), Is.False);
            Assert.That(em.GetComponent<KiasIntegratedComponent>(direct).Direct, Is.True);
        });
        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() =>
        {
            em.GetComponent<KiasRoomScannerComponent>(first).Range = 10;
            em.System<KiasCrewSystem>().RebuildCoverage(map.Grid);
            Assert.That(em.GetComponent<KiasIntegratedComponent>(alarm).Scanner, Is.EqualTo(first));
        });
        await pair.RunTicksSync(30);
        uint revision = 0;
        await pair.Server.WaitAssertion(() => revision = em.GetComponent<KiasGridComponent>(map.Grid).Revision);
        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() => Assert.That(em.GetComponent<KiasGridComponent>(map.Grid).Revision, Is.EqualTo(revision)));
        await pair.Server.WaitAssertion(() => em.System<SharedTransformSystem>().Unanchor(first));
        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<KiasIntegratedComponent>(alarm).Scanner, Is.Null);
            em.System<SharedTransformSystem>().AnchorEntity(first);
        });
        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<KiasIntegratedComponent>(alarm).Scanner, Is.EqualTo(first));
            Assert.That(em.System<ItemSlotsSystem>().TryEject(first, "kias-module-3", null, out _), Is.True);
            Assert.That(em.System<ItemSlotsSystem>().TryEject(first, "kias-module-6", null, out _), Is.True);
        });
        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(em.GetComponent<KiasIntegratedComponent>(alarm).Scanner, Is.Null);
            Assert.That(em.GetComponent<KiasIntegratedComponent>(direct).Direct, Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
