using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Forge.KIAS;
using Content.Client._Forge.KIAS.Controllers;
using Content.Shared._Forge.KIAS;
using Content.Shared._Forge.KIAS.Controllers;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
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
            populated.Profiles.Add(new() { Id = "Power" });
            populated.Devices.Add(new() { Name = new string('W', 200), Profile = "Power" });
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
                if (window == programmer)
                {
                    var deviceButton = Descendants(programmer).OfType<Button>().Single(button => button.Text?.StartsWith(populated.Devices[0].Name) == true);
                    var palette = deviceButton.Parent!;
                    var scroll = (ScrollContainer) palette.Parent!;
                    Assert.That(deviceButton.Size.X, Is.InRange(100f, 250f));
                    Assert.That(palette.Size.X, Is.LessThanOrEqualTo(scroll.Size.X));
                    Assert.That(deviceButton.Label.GlobalPosition.X, Is.InRange(scroll.GlobalPosition.X, scroll.GlobalPosition.X + scroll.Size.X));
                    Assert.That(deviceButton.Label.Size.X, Is.GreaterThan(100));
                    Assert.That(deviceButton.Label.Align, Is.EqualTo(Label.AlignMode.Left));
                    Assert.That(deviceButton.ToolTip, Is.EqualTo(deviceButton.Text));
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
