using System.Linq;
using Content.Server._Forge.KIAS;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.NodeContainer;
using Content.Shared.Power;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasRelayTests
{
    [Test]
    public async Task SelectedChannelReallySplitsAndReconnectsNetworks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        var kias = em.System<KiasSystem>();
        var relays = em.System<KiasRelaySystem>();
        EntityUid relay = default;
        EntityUid device = default;
        var cables = new EntityUid[3, 9];
        await pair.Server.WaitAssertion(() =>
        {
            var maps = em.System<SharedMapSystem>();
            var types = new[] { "CableHV", "CableMV", "CableApcExtension" };
            for (var x = 0; x < 9; x++)
            {
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, 0), map.Tile.Tile);
                em.SpawnEntity("KiasDataCable", new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
                for (var channel = 0; channel < 3; channel++)
                    cables[channel, x] = em.SpawnEntity(types[channel], new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
            }
            EntityUid Spawn(string prototype, int x)
            {
                var uid = em.SpawnEntity(prototype, new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
                em.RemoveComponent<ApcPowerReceiverComponent>(uid);
                return uid;
            }
            Spawn("KiasCore", 0);
            relay = Spawn("KiasRelay", 4);
            device = Spawn("KiasCrewServer", 8);
            kias.Rebuild(map.Grid);
        });
        object? Group(int channel, int x) => em.GetComponent<NodeContainerComponent>(cables[channel, x]).Nodes.Values.Single().NodeGroup;
        await pair.RunTicksSync(3);
        foreach (var selected in Enum.GetValues<CableType>())
        {
            await pair.Server.WaitAssertion(() =>
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    Assert.That(Group(channel, 0), Is.Not.Null);
                    Assert.That(Group(channel, 0), Is.SameAs(Group(channel, 8)));
                }
                Assert.That(kias.IsOnline(device), Is.True);
                Assert.That(relays.SetChannel(relay, selected), Is.True);
                Assert.That(relays.SetClosed(relay, false), Is.True);
            });
            await pair.RunTicksSync(3);
            await pair.Server.WaitAssertion(() =>
            {
                kias.Rebuild(map.Grid);
                Assert.That(kias.IsOnline(device), Is.EqualTo(selected != CableType.Data));
                for (var channel = 0; channel < 3; channel++)
                {
                    if (channel == (int) selected)
                        Assert.That(Group(channel, 0), Is.Not.SameAs(Group(channel, 8)));
                    else
                        Assert.That(Group(channel, 0), Is.SameAs(Group(channel, 8)));
                }
                Assert.That(relays.SetClosed(relay, true), Is.True);
            });
            await pair.RunTicksSync(3);
        }
        await pair.CleanReturnAsync();
    }
}
