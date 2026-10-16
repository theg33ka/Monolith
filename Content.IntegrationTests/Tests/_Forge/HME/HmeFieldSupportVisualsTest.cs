using System.Numerics;
using System.Collections.Generic;
using Content.Shared._Forge.HME;
using Content.Shared.Storage;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Forge.HME;

[TestFixture]
public sealed class HmeFieldSupportVisualsTest
{
    [Test]
    public async Task BlossomDeploysThenHoversAndCancelsOnFold()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var entities = client.ResolveDependency<IEntityManager>();
        var serverEntities = pair.Server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        EntityUid serverUid = default;
        await pair.Server.WaitPost(() =>
        {
            serverUid = serverEntities.SpawnEntity("HMEStasisBlossomUnit", new MapCoordinates(Vector2.Zero, map.MapId));
        });
        await pair.RunTicksSync(5);
        var uid = entities.GetEntity(serverEntities.GetNetEntity(serverUid));

        await client.WaitAssertion(() =>
        {
            var sprite = entities.GetComponent<SpriteComponent>(uid);
            var appearance = client.System<AppearanceSystem>();
            var animation = client.System<AnimationPlayerSystem>();
            var baseLayer = sprite.LayerMapGet("base");
            var screen = sprite.LayerMapGet("screen");

            void SetState(HmeStasisBlossomVisualState state)
            {
                appearance.SetData(uid, HmeStasisBlossomVisuals.State, state);
                appearance.FrameUpdate(0f);
                animation.FrameUpdate(0.01f);
            }

            SetState(HmeStasisBlossomVisualState.Deployed);
            Assert.That(sprite[baseLayer].RsiState.Name, Is.EqualTo("deploying"));
            Assert.That(sprite[screen].RsiState.Name, Is.EqualTo("deploying-screen"));
            animation.FrameUpdate(1.4f);
            Assert.That(sprite[baseLayer].RsiState.Name, Is.EqualTo("active"));
            Assert.That(sprite[screen].RsiState.Name, Is.EqualTo("active-screen"));
            Assert.That(sprite[baseLayer].AutoAnimated, Is.True);
            Assert.That(sprite[screen].AutoAnimated, Is.True);

            SetState(HmeStasisBlossomVisualState.Attached);
            Assert.That(sprite[baseLayer].RsiState.Name, Is.EqualTo("compact"));
            Assert.That(sprite[screen].Visible, Is.False);
            SetState(HmeStasisBlossomVisualState.Deployed);
            animation.FrameUpdate(0.2f);
            SetState(HmeStasisBlossomVisualState.Attached);
            animation.FrameUpdate(2f);
            Assert.That(sprite[baseLayer].RsiState.Name, Is.EqualTo("compact"));
            Assert.That(sprite[screen].Visible, Is.False);

            SetState(HmeStasisBlossomVisualState.Deployed);
            animation.FrameUpdate(1.4f);
            Assert.That(sprite[baseLayer].RsiState.Name, Is.EqualTo("active"));
            Assert.That(sprite[screen].Visible, Is.True);
        });
        await pair.Server.WaitPost(() => serverEntities.DeleteEntity(serverUid));
        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InfusionBeakerCountUpdatesOnInsertionAndRemoval()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var uid = entities.SpawnEntity("HMESmartInfusionStand", MapCoordinates.Nullspace);
            var storage = entities.GetComponent<StorageComponent>(uid);
            var appearance = server.System<SharedAppearanceSystem>();
            var containers = server.System<SharedContainerSystem>();
            var beakers = new List<EntityUid>();
            for (var i = 1; i <= 3; i++)
            {
                var beaker = entities.SpawnEntity("Beaker", MapCoordinates.Nullspace);
                beakers.Add(beaker);
                Assert.That(containers.Insert(beaker, storage.Container), Is.True);
                Assert.That(appearance.TryGetData<int>(uid, HmeInfusionStandVisuals.BeakerCount, out var count), Is.True);
                Assert.That(count, Is.EqualTo(i));
            }
            for (var i = 2; i >= 0; i--)
            {
                Assert.That(containers.Remove(beakers[i], storage.Container), Is.True);
                Assert.That(appearance.TryGetData<int>(uid, HmeInfusionStandVisuals.BeakerCount, out var count), Is.True);
                Assert.That(count, Is.EqualTo(i));
                entities.DeleteEntity(beakers[i]);
            }
            entities.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}
