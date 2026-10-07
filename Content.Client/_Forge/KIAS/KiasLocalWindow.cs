using System.Numerics;
using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared._Forge.KIAS;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Forge.KIAS;

public class KiasLocalWindow : FancyWindow
{
    public event Action<KiasDeviceSettingsMessage>? SettingsChanged;
    public event Action<KiasServiceMode>? ModeChanged;
    public event Action<string>? MessageChanged;
    public event Action? RefreshRequested;
    private readonly Label _details = new();
    private readonly LineEdit _color = new();
    private readonly LineEdit _brightness = new();
    private readonly BoxContainer _logRows = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly LineEdit _room = new();
    private readonly LineEdit _range = new();
    private readonly LineEdit _group = new();
    private readonly LineEdit _message = new();
    private readonly CheckBox _locked = new();
    private readonly OptionButton _mode = new();
    private readonly OptionButton _page = new();
    private readonly LineEdit _filter = new();
    private readonly Button _save = new();
    private BoundUserInterfaceState? _state;
    private readonly BoxContainer _box;
    private KiasCoverageNavControl? _radar;

    public KiasLocalWindow()
    {
        SetSize = new Vector2(430, 380);
        Resizable = true;
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        _box = box;
        XamlChildren.Add(box);
        var scroll = new ScrollContainer { VerticalExpand = true };
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        content.AddChild(_details);
        content.AddChild(_logRows);
        scroll.AddChild(content);
        box.AddChild(scroll);
        _room.PlaceHolder = Loc.GetString("kias-mode-room");
        _group.PlaceHolder = Loc.GetString("kias-mode-group");
        _message.PlaceHolder = Loc.GetString("kias-custom-message");
        _range.PlaceHolder = Loc.GetString("kias-sensor-range");
        _locked.Text = Loc.GetString("kias-lock-registration");
        _color.PlaceHolder = Loc.GetString("kias-light-color");
        _brightness.PlaceHolder = Loc.GetString("kias-light-brightness");
        box.AddChild(_color);
        box.AddChild(_brightness);
        _filter.PlaceHolder = Loc.GetString("kias-log-filter");
        foreach (var mode in Enum.GetValues<KiasServiceMode>())
            _mode.AddItem(Loc.GetString($"kias-mode-{mode.ToString().ToLowerInvariant()}"), (int) mode);
        _mode.OnItemSelected += args => { _mode.SelectId(args.Id); ModeChanged?.Invoke((KiasServiceMode) args.Id); };
        foreach (var page in Enum.GetValues<KiasDisplayPage>())
            _page.AddItem(Loc.GetString($"kias-page-{page.ToString().ToLowerInvariant()}"), (int) page);
        _page.OnItemSelected += args => _page.SelectId(args.Id);
        _filter.OnTextChanged += _ => { if (_state != null) UpdateState(_state); };
        foreach (var control in new Robust.Client.UserInterface.Control[] { _filter, _mode, _page, _room, _range, _group, _message, _locked })
            box.AddChild(control);
        _save.Text = Loc.GetString("kias-save");
        _save.OnPressed += _ => Save();
        box.AddChild(_save);
        var refresh = new Button { Text = Loc.GetString("kias-refresh") };
        refresh.OnPressed += _ => RefreshRequested?.Invoke();
        box.AddChild(refresh);
    }

    public void UpdateState(BoundUserInterfaceState state)
    {
        _state = state;
        var geometry = state switch { KiasSensorState sensor => sensor.Geometry, KiasServiceState service => service.Geometry, _ => null };
        if (geometry is { Large: true } && _radar == null)
        {
            _radar = new KiasCoverageNavControl { MinSize = new Vector2(360, 300), VerticalExpand = true };
            _box.AddChild(_radar);
            SetSize = new Vector2(600, 680);
        }
        if (_radar != null)
        {
            _radar.Visible = geometry is { Large: true };
            _radar.SetCoverage(geometry);
        }
        _mode.Visible = state is KiasServiceState;
        _page.Visible = state is KiasWallState;
        _filter.Visible = state is KiasRecorderState;
        _room.Visible = state is KiasLocalState;
        _range.Visible = state is KiasScannerState or KiasSensorState or KiasWirelessState;
        _group.Visible = state is KiasSpeakerState or KiasWirelessState or KiasLightState;
        _color.Visible = _brightness.Visible = state is KiasLightState;
        _logRows.Visible = state is KiasRecorderState;
        _message.Visible = state is KiasSpeakerState or KiasServiceState;
        _locked.Visible = state is KiasCrewState;
        _save.Visible = state is not KiasRecorderState;
        if (state is KiasLocalState device)
        {
            Title = device.Name;
            _details.Text = Loc.GetString($"kias-status-{device.Status.ToString().ToLowerInvariant()}");
            if (!_room.HasKeyboardFocus()) _room.Text = device.Room;
        }
        switch (state)
        {
            case KiasWallState wall:
                Title = Loc.GetString("ent-KiasDisplay");
                _details.Text = $"Entities: {wall.Entities} // Crew: {wall.Crew}\n{wall.Details}";
                _page.SelectId((int) wall.Page);
                break;
            case KiasRecorderState recorder:
                Title = Loc.GetString("ent-KiasRecorder");
                _details.Text = Loc.GetString(recorder.Online ? "kias-online" : "kias-status-offline");
                _logRows.RemoveAllChildren();
                foreach (var entry in recorder.Entries.Where(entry => entry.Contains(_filter.Text, StringComparison.OrdinalIgnoreCase)))
                    _logRows.AddChild(new Label { Text = entry, Modulate = entry.Contains("[!]") ? Robust.Shared.Maths.Color.OrangeRed : Robust.Shared.Maths.Color.White });
                break;
            case KiasServiceState service:
                Title = Loc.GetString("ent-KiasServiceTool");
                _mode.SelectId((int) service.Mode);
                _details.Text = Loc.GetString("kias-service-target", ("source", service.SourceName), ("target", service.TargetName)) + "\n" + service.Details;
                if (!_message.HasKeyboardFocus()) _message.Text = service.Message;
                break;
            case KiasScannerState scanner:
                var modules = new[] { KiasScannerModules.Motion, KiasScannerModules.Identity, KiasScannerModules.Biometric, KiasScannerModules.Radiation, KiasScannerModules.Spectral, KiasScannerModules.Connector, KiasScannerModules.Optical, KiasScannerModules.Threat };
                _details.Text += $"\n{Loc.GetString("kias-scanner-modules")}: {string.Join(", ", modules.Where(module => (scanner.Modules & module) != 0).Select(module => Loc.GetString($"kias-module-{module.ToString().ToLowerInvariant()}")))}";
                if (!_range.HasKeyboardFocus()) _range.Text = scanner.Range.ToString();
                break;
            case KiasSensorState sensor:
                _details.Text += $"\n{Loc.GetString("kias-sensor-range")}: {sensor.Range} m / {sensor.Arc}°";
                if (!_range.HasKeyboardFocus()) _range.Text = sensor.Range.ToString();
                break;
            case KiasCrewState crew:
                _details.Text += $"\nCrew: {crew.DetectedCrew}\n{string.Join("\n", crew.Registered)}";
                _locked.Pressed = crew.Locked;
                break;
            case KiasSpeakerState speaker:
                if (!_group.HasKeyboardFocus()) _group.Text = speaker.Group;
                if (!_message.HasKeyboardFocus()) _message.Text = speaker.Message;
                break;
            case KiasResourceState resource:
                _details.Text += "\n" + resource.Details;
                break;
            case KiasWirelessState wireless:
                _details.Text += "\n" + Loc.GetString("kias-wireless-trusted", ("devices", string.Join(", ", wireless.TrustedTransmitters)));
                if (!_group.HasKeyboardFocus()) _group.Text = wireless.Channel;
                if (!_range.HasKeyboardFocus()) _range.Text = wireless.Range.ToString();
                break;
            case KiasLightState light:
                if (!_group.HasKeyboardFocus()) _group.Text = light.Group;
                if (!_color.HasKeyboardFocus()) _color.Text = light.Color;
                if (!_brightness.HasKeyboardFocus()) _brightness.Text = light.Brightness.ToString();
                break;
        }
    }

    private void Save()
    {
        if (_state is KiasServiceState)
        {
            MessageChanged?.Invoke(_message.Text);
            return;
        }
        var range = 0f;
        var brightness = 0.8f;
        if (_brightness.Visible && !float.TryParse(_brightness.Text, out brightness)) return;
        if (_range.Visible && !float.TryParse(_range.Text, out range)) return;
        SettingsChanged?.Invoke(new KiasDeviceSettingsMessage { Room = _room.Text, Group = _group.Text, Color = _color.Text, Brightness = brightness,
            Message = _message.Text, Range = range, LockRegistration = _locked.Pressed, Page = (KiasDisplayPage) _page.SelectedId });
    }
}

public sealed class KiasWallWindow : KiasLocalWindow;
public sealed class KiasRecorderWindow : KiasLocalWindow;
public sealed class KiasServiceWindow : KiasLocalWindow;
public sealed class KiasScannerWindow : KiasLocalWindow;
public sealed class KiasSensorWindow : KiasLocalWindow;
public sealed class KiasCrewWindow : KiasLocalWindow;
public sealed class KiasSpeakerWindow : KiasLocalWindow;
