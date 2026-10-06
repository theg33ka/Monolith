using Content.Shared._Forge.KIAS;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Forge.KIAS;

[UsedImplicitly]
public sealed class KiasBoundUserInterface : BoundUserInterface
{
    private KiasWindow? _window;

    public KiasBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<KiasWindow>();
        _window.RefreshRequested += () => SendMessage(new KiasRefreshMessage());
        _window.MessageChanged += message => SendMessage(new KiasSetMessage(message));
        _window.ProtocolChanged += message => SendMessage(message);
        _window.ControlRequested += reset => SendMessage(new KiasControlMessage { Reset = reset });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is KiasUiState kias)
            _window?.UpdateState(kias);
    }
}
