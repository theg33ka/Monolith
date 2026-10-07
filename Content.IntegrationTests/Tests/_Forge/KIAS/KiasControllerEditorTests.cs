#pragma warning disable RA0002
using Content.Server._Forge.KIAS;
using Content.Server._Forge.KIAS.Controllers;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS.Controllers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._Forge.KIAS;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasControllerEditorTests
{
    [Test]
    public async Task DraftLeaseAtomicWriteStaleMessagesRangeAndDirtyEjection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var em = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var coordinates = new EntityCoordinates(map.Grid, .5f, .5f);
            EntityUid Spawn(string prototype)
            {
                var uid = em.SpawnEntity(prototype, coordinates);
                em.RemoveComponent<ApcPowerReceiverComponent>(uid); return uid;
            }
            Spawn("KiasCore"); Spawn("KiasDataCable");
            var programmer = Spawn("KiasControllerProgrammer");
            var card = Spawn("KiasProgrammableController");
            var actor = Spawn("MobHuman"); var other = Spawn("MobHuman");
            em.System<KiasSystem>().Rebuild(map.Grid);
            var slots = em.System<ItemSlotsSystem>();
            Assert.That(slots.TryInsert(programmer, KiasControllerProgrammerComponent.SlotId, card, actor), Is.True);
            var ui = em.System<UserInterfaceSystem>(); var editor = em.System<KiasControllerUiSystem>();
            Assert.That(ui.TryOpenUi(programmer, KiasControllerUiKey.Programmer, actor), Is.True);
            Assert.That(ui.TryOpenUi(programmer, KiasControllerUiKey.Programmer, other), Is.False, "Only one writer may own the draft lease.");
            var draft = em.GetComponent<KiasControllerProgrammerComponent>(programmer);
            var stored = em.GetComponent<KiasControllerCardComponent>(card);
            bool Edit(KiasGraphEdit action, string text = "", uint? revision = null) => editor.Edit((programmer, draft), actor,
                new() { Edit = action, Text = text, Revision = revision ?? draft.DraftRevision });
            Assert.That(Edit(KiasGraphEdit.Rename, "Written program"), Is.True);
            Assert.That(stored.Program.Name, Is.EqualTo("Controller"));
            Assert.That(Edit(KiasGraphEdit.Rename, "stale", draft.DraftRevision - 1), Is.False);
            Assert.That(Edit(KiasGraphEdit.Eject), Is.False);
            Assert.That(Edit(KiasGraphEdit.Write), Is.True);
            Assert.That(stored.Program.Name, Is.EqualTo("Written program")); Assert.That(stored.Revision, Is.EqualTo(1));
            Assert.That(Edit(KiasGraphEdit.Rename, "Discard me"), Is.True);
            Assert.That(Edit(KiasGraphEdit.Discard), Is.True); Assert.That(draft.Draft!.Name, Is.EqualTo(stored.Program.Name));
            Assert.That(editor.Edit((programmer, draft), actor, new() { Revision = draft.DraftRevision, Edit = KiasGraphEdit.Add,
                Kind = KiasNodeKind.Specific, Profile = "Speaker", Binding = em.GetNetEntity(actor) }), Is.False);
            em.System<SharedTransformSystem>().SetCoordinates(actor, new EntityCoordinates(map.Grid, 100, 100));
            Assert.That(Edit(KiasGraphEdit.Write), Is.False, "A UI handle does not authorize remote writes.");
            em.System<SharedTransformSystem>().SetCoordinates(actor, coordinates);
            em.EnsureComponent<KiasClaimComponent>(map.Grid).Owner = new Robust.Shared.Network.NetUserId(Guid.NewGuid());
            Assert.That(Edit(KiasGraphEdit.Rename, "unauthorized"), Is.False);
            em.RemoveComponent<KiasClaimComponent>(map.Grid);
            ui.CloseUi(programmer, KiasControllerUiKey.Programmer, actor);
            Assert.That(Edit(KiasGraphEdit.Write), Is.False);
            Assert.That(slots.TryEject(programmer, KiasControllerProgrammerComponent.SlotId, actor, out _), Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
