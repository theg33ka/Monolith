using System.Numerics;
using Content.Client._Forge.KIAS;
using Content.Client._Forge.KIAS.Controllers;
using Content.Shared._Forge.KIAS;
using Content.Shared._Forge.KIAS.Controllers;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Forge.KIAS;

[TestFixture]
public sealed class KiasControllerLayoutTests
{
    [Test]
    public async Task NativeWindowsArrangeWithEmptyAndPopulatedGraphs()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Client.WaitAssertion(() =>
        {
            using var management = new KiasWindow();
            management.UpdateState(new KiasManagementState { Online = true, Automation = "2 cards" });
            using var programmer = new KiasControllerWindow();
            programmer.UpdateState(new KiasControllerEditorState());
            var populated = new KiasControllerEditorState { Online = true, Editing = true, HasCard = true, Revision = 5, Name = "Layout" };
            populated.Nodes.Add(new() { Id = 1, Kind = KiasNodeKind.OnStart, Ports = KiasGraphCatalog.InternalPorts(KiasNodeKind.OnStart) });
            populated.Nodes.Add(new() { Id = 2, Kind = KiasNodeKind.Timer, X = 350, Y = 30, Ports = KiasGraphCatalog.InternalPorts(KiasNodeKind.Timer) });
            populated.Wires.Add(new() { FromNode = 1, FromPort = "Started", ToNode = 2, ToPort = "Trigger" });
            programmer.UpdateState(populated);
            using var rack = new KiasControllerRackWindow();
            var rackState = new KiasControllerRackState();
            for (var i = 0; i < 8; i++) rackState.Slots.Add(new());
            rack.UpdateState(rackState);
            foreach (var window in new Robust.Client.UserInterface.Control[] { management, programmer, rack })
            foreach (var size in new[] { new Vector2(1200, 720), new Vector2(850, 500) })
            {
                window.Measure(size);
                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                Assert.That(float.IsFinite(window.DesiredSize.X) && float.IsFinite(window.DesiredSize.Y), Is.True);
            }
        });
        await pair.CleanReturnAsync();
    }
}
