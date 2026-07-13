using Content.Shared._Forge.Horizon.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Forge.Horizon.UI;

public sealed class HorizonConsoleBoundUserInterface : BoundUserInterface
{
    private HorizonConsoleWindow? _window;

    public HorizonConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new HorizonConsoleWindow
        {
            Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName,
        };
        _window.RefreshButton.OnPressed += _ => SendMessage(new HorizonConsoleRefreshMessage());
        _window.HandoffButton.OnPressed += _ => SendMessage(new HorizonConsoleHandoffMessage());
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is HorizonConsoleBoundUserInterfaceState horizonState)
            _window?.UpdateState(horizonState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
