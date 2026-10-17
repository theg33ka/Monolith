using System;
using System.Linq;
using Content.Server.Hands.Systems;
using Content.Server.Medical.Components;
using Content.Shared._Forge.HME;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Labels.Components;
using Content.Shared.Interaction;
using Content.Shared.Standing;
using Content.Shared.MedicalScanner;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Forge.HME;

[TestFixture]
public sealed class HmeStasisPatientTest
{
    [Test]
    public async Task DeployedPatientIsHiddenInjectableAndLabelledWithOneBatteryIndicator()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var entities = server.ResolveDependency<IEntityManager>();
        var clientEntities = client.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        EntityUid user = default, patient = default, bag = default, syringe = default;
        var patientName = "HME test patient";
        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            patient = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.System<MetaDataSystem>().SetEntityName(patient, patientName);
            server.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false);
            bag = entities.SpawnEntity("HMEStasisBlossomUnit", map.GridCoords);
            entities.GetComponent<HmeStasisBlossomComponent>(bag).AttachDelay = TimeSpan.Zero;
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.GetContainer(bag, "cell_slot").ContainedEntities, Has.Count.EqualTo(1));
            Assert.That(server.System<HandsSystem>().TryPickupAnyHand(user, bag), Is.True);
            Assert.That(server.System<SharedInteractionSystem>().InteractUsing(user, bag, patient, map.GridCoords), Is.True);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.GetComponent<HmeStasisBlossomComponent>(bag).AttachedTarget, Is.EqualTo(patient));
            Assert.That(entities.GetComponent<LabelComponent>(bag).CurrentLabel, Does.Contain(patientName));
            var scan = new InteractHandEvent(user, bag);
            entities.EventBus.RaiseLocalEvent(bag, scan);
            Assert.That(scan.Handled, Is.True);
            Assert.That(entities.GetComponent<HealthAnalyzerComponent>(bag).ScannedEntity, Is.EqualTo(patient));
            Assert.That(server.System<SharedUserInterfaceSystem>().IsUiOpen(bag, HealthAnalyzerUiKey.Key, user), Is.True);
            Assert.That(entities.GetComponent<HmeStasisBlossomComponent>(bag).AttachedTarget, Is.EqualTo(patient));
            var activate = new ActivateInWorldEvent(user, bag, true);
            entities.EventBus.RaiseLocalEvent(bag, activate);
            Assert.That(entities.GetComponent<HmeStasisBlossomComponent>(bag).Deployed, Is.True);
            server.System<SharedUserInterfaceSystem>().CloseUi(bag, HealthAnalyzerUiKey.Key);
            scan = new InteractHandEvent(user, bag);
            entities.EventBus.RaiseLocalEvent(bag, scan);
            Assert.That(server.System<SharedUserInterfaceSystem>().IsUiOpen(bag, HealthAnalyzerUiKey.Key, user), Is.True);
            Assert.That(entities.GetComponent<Robust.Server.GameObjects.PointLightComponent>(bag).Enabled, Is.True);
            var examine = server.System<ExamineSystemShared>().GetExamineText(bag, user).ToString();
            Assert.That(examine, Does.Contain(patientName));
            Assert.That(examine.Count(character => character == '%'), Is.EqualTo(1));
        });
        await pair.RunTicksSync(10);
        var clientPatient = clientEntities.GetEntity(entities.GetNetEntity(patient));
        await client.WaitAssertion(() => Assert.That(clientEntities.GetComponent<SpriteComponent>(clientPatient).Visible, Is.False));
        await server.WaitAssertion(() =>
        {
            syringe = entities.SpawnEntity("SyringeBicaridine", map.GridCoords);
            entities.GetComponent<InjectorComponent>(syringe).Delay = TimeSpan.Zero;
            Assert.That(server.System<HandsSystem>().TryPickupAnyHand(user, syringe), Is.True);
            Assert.That(server.System<SharedInteractionSystem>().InteractUsing(user, syringe, bag, map.GridCoords), Is.True);
        });
        await pair.RunTicksSync(90);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<SharedSolutionContainerSystem>().TryGetSolution(syringe, "injector", out _, out var solution), Is.True);
            Assert.That(solution.Volume.Float(), Is.LessThan(15f));
            var fold = new ActivateInWorldEvent(user, bag, true);
            entities.EventBus.RaiseLocalEvent(bag, fold);
            Assert.That(entities.HasComponent<HmeStasisPatientComponent>(patient), Is.False);
            Assert.That(entities.GetComponent<Robust.Server.GameObjects.PointLightComponent>(bag).Enabled, Is.False);
        });
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => Assert.That(clientEntities.GetComponent<SpriteComponent>(clientPatient).Visible, Is.True));
        await server.WaitPost(() =>
        {
            var deploy = new ActivateInWorldEvent(user, bag, true);
            entities.EventBus.RaiseLocalEvent(bag, deploy);
        });
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => Assert.That(clientEntities.GetComponent<SpriteComponent>(clientPatient).Visible, Is.False));
        await server.WaitPost(() => entities.DeleteEntity(bag));
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => Assert.That(clientEntities.GetComponent<SpriteComponent>(clientPatient).Visible, Is.True));
        await server.WaitPost(() =>
        {
            entities.DeleteEntity(syringe);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(user);
        });
        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }
}
