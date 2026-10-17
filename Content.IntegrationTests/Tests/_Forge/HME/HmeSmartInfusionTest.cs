using System;
using Content.Server._Forge.HME;
using Content.Shared._Forge.HME;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Forge.HME;

[TestFixture]
public sealed class HmeSmartInfusionTest
{
    [Test]
    public async Task CriticalPatientReceivesEpinephrineAndWoundMedicationWithoutAccumulation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var stand = entities.SpawnEntity("HMESmartInfusionStand", map.GridCoords);
            var patient = entities.SpawnEntity("MobHuman", map.GridCoords);
            var storage = entities.GetComponent<StorageComponent>(stand);
            var solutions = server.System<SharedSolutionContainerSystem>();
            var infusion = server.System<HmeSmartInfusionSystem>();
            var component = entities.GetComponent<HmeSmartInfusionStandComponent>(stand);
            var sources = new EntityUid[2];
            var reagents = new[] { "Bicaridine", "Epinephrine" };
            for (var i = 0; i < reagents.Length; i++)
            {
                sources[i] = entities.SpawnEntity("Beaker", map.GridCoords);
                Assert.That(solutions.TryGetDrainableSolution(sources[i], out var source, out _), Is.True);
                Assert.That(solutions.TryAddReagent(source.Value, reagents[i], FixedPoint2.New(30)), Is.True);
                Assert.That(server.System<SharedContainerSystem>().Insert(sources[i], storage.Container), Is.True);
            }

            server.System<DamageableSystem>().SetDamage(patient, entities.GetComponent<DamageableComponent>(patient),
                new DamageSpecifier { DamageDict = { ["Piercing"] = 90, ["Asphyxiation"] = 60 } });
            server.System<MobStateSystem>().ChangeMobState(patient, MobState.Critical);
            for (var i = 0; i < 10; i++)
            {
                component.NextUpdate = TimeSpan.Zero;
                infusion.Update(0f);
            }

            Assert.That(solutions.TryGetInjectableSolution(patient, out var target, out var chemical), Is.True);
            Assert.That(chemical.GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(component.TransferAmount));
            Assert.That(chemical.GetTotalPrototypeQuantity("Bicaridine"), Is.EqualTo(component.TransferAmount));

            solutions.RemoveReagent(target.Value, "Epinephrine", component.TransferAmount);
            server.System<MobStateSystem>().ChangeMobState(patient, MobState.Alive);
            server.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false);
            component.NextUpdate = TimeSpan.Zero;
            infusion.Update(0f);
            Assert.That(chemical.GetTotalPrototypeQuantity("Epinephrine"), Is.EqualTo(FixedPoint2.Zero));

            entities.DeleteEntity(stand);
            foreach (var source in sources)
                if (!entities.Deleted(source))
                    entities.DeleteEntity(source);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }
}
