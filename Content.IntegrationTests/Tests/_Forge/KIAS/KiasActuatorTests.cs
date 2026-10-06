using System.Linq;
using Content.Server._Forge.KIAS;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.Light.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasActuatorTests
{
    [Test]
    public async Task LightGroupsCartridgeConsumptionAndHardOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var maps = em.System<SharedMapSystem>();
            for (var x = 0; x < 5; x++)
            {
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
                em.SpawnEntity("KiasDataCable", new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
            }
            EntityUid Spawn(string proto, int x)
            {
                var uid = em.SpawnEntity(proto, new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid);
                return uid;
            }
            var core = Spawn("KiasCore", 0);
            Spawn("KiasAtmosServer", 1);
            var controller = Spawn("KiasLightController", 2);
            var suppression = Spawn("KiasSuppression", 3);
            var lamp = Spawn("PoweredlightEmpty", 4);
            var otherLamp = Spawn("PoweredlightEmpty", 4);
            em.AddComponent<KiasLightGroupComponent>(lamp).Group = "EMERGENCY";
            em.AddComponent<KiasLightGroupComponent>(otherLamp).Group = "ENGINEERING";
            em.GetComponent<KiasLightControllerComponent>(controller).Group = "EMERGENCY";
            var kias = em.System<KiasSystem>();
            kias.Rebuild(map.Grid);
            var actuators = em.System<KiasActuatorSystem>();
            actuators.SetLights(controller, false);
            var on = em.GetComponent<PoweredLightComponent>(lamp).On;
            var otherOn = em.GetComponent<PoweredLightComponent>(otherLamp).On;
            Assert.That(on, Is.False);
            Assert.That(otherOn, Is.True);
            Assert.That(actuators.Suppress(suppression), Is.True);
            Assert.That(actuators.Suppress(suppression), Is.False);
            var containers = em.System<SharedContainerSystem>();
            containers.TryGetContainer(suppression, "kias-cartridge", out var slot);
            foreach (var old in slot!.ContainedEntities.ToArray())
                containers.Remove(old, slot);
            var cartridge = em.SpawnEntity("KiasSuppressionCartridge", new EntityCoordinates(map.Grid, 3.5f, 0.5f));
            containers.Insert(cartridge, slot);
            kias.SetEnabled(core, false);
            actuators.SetLights(controller, true);
            on = em.GetComponent<PoweredLightComponent>(lamp).On;
            Assert.That(on, Is.False);
            Assert.That(actuators.Suppress(suppression), Is.False);
            Assert.That(slot.Contains(cartridge), Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
