using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Forge.KIAS.Controllers;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Forge.KIAS.Controllers;

public sealed class KiasControllerWindow : FancyWindow
{
    public event Action<KiasControllerEditMessage>? Edited;
    private readonly KiasGraphCanvas _canvas = new();
    private readonly BoxContainer _palette = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _settings = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly LineEdit _search = new(), _name = new();
    private readonly Label _status = new();
    private readonly OptionButton _presets = new();
    private readonly OptionButton _legacy = new();
    private readonly CheckBox _enabled = new();
    private bool _updating;
    private readonly Button _write, _eject, _discard;
    private KiasControllerEditorState _state = new();
    private bool _pending;
    public KiasControllerWindow()
    {
        Title = Loc.GetString("kias-controller-editor-title");
        SetSize = new Vector2(1200, 720); MinSize = new Vector2(850, 500); Resizable = true;
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        XamlChildren.Add(root);
        var toolbar = new BoxContainer(); root.AddChild(toolbar);
        _name.HorizontalExpand = true; toolbar.AddChild(_name);
        toolbar.AddChild(Button("kias-controller-rename", () => Send(new() { Edit = KiasGraphEdit.Rename, Text = _name.Text })));
        var templates = new BoxContainer(); root.AddChild(templates);
        templates.AddChild(_presets);
        _presets.OnItemSelected += args => _presets.SelectId(args.Id);
        templates.AddChild(Button("kias-controller-load-preset", () =>
        {
            if (_presets.SelectedId is >= 0 && _presets.SelectedId < _state.Presets.Count)
                Send(new() { Edit = KiasGraphEdit.Preset, Text = _state.Presets[_presets.SelectedId] });
        }));
        templates.AddChild(_legacy); _legacy.OnItemSelected += args => _legacy.SelectId(args.Id);
        templates.AddChild(Button("kias-controller-import-legacy", () =>
        {
            if (_legacy.SelectedId >= 0 && _legacy.SelectedId < _state.Legacy.Count)
                Send(new() { Edit = KiasGraphEdit.ImportLegacy, Node = _legacy.SelectedId });
        }));
        _enabled.Text = Loc.GetString("kias-protocol-enabled"); templates.AddChild(_enabled);
        _enabled.OnToggled += _ => { if (!_updating) Send(new() { Edit = KiasGraphEdit.Enabled, Enabled = _enabled.Pressed }); };
        _write = Button("kias-controller-write", () => Send(new() { Edit = KiasGraphEdit.Write })); toolbar.AddChild(_write);
        _discard = Button("kias-controller-discard", () => Send(new() { Edit = KiasGraphEdit.Discard })); toolbar.AddChild(_discard);
        _eject = Button("kias-controller-eject", () => Send(new() { Edit = KiasGraphEdit.Eject })); toolbar.AddChild(_eject);
        toolbar.AddChild(Button("kias-refresh", () => Send(new() { Edit = KiasGraphEdit.Refresh })));
        root.AddChild(_status);
        root.AddChild(new Label { Text = Loc.GetString("kias-controller-editor-help") });
        var body = new BoxContainer { VerticalExpand = true }; root.AddChild(body);
        var sidebar = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 215, MaxWidth = 250 };
        body.AddChild(sidebar);
        _search.PlaceHolder = Loc.GetString("kias-controller-search"); sidebar.AddChild(_search);
        _search.OnTextChanged += _ => RebuildPalette();
        var paletteScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false }; paletteScroll.AddChild(_palette); sidebar.AddChild(paletteScroll);
        body.AddChild(_canvas);
        var settingsScroll = new ScrollContainer { MinWidth = 205, MaxWidth = 240 }; settingsScroll.AddChild(_settings); body.AddChild(settingsScroll);
        _canvas.Edited += Send; _canvas.Selected += SelectNode;
    }
    private static Button Button(string key, Action action)
    {
        var button = new Button { Text = Loc.GetString(key) }; button.OnPressed += _ => action(); return button;
    }
    private void Send(KiasControllerEditMessage message)
    {
        if (_pending && message.Edit != KiasGraphEdit.Refresh) return;
        message.Revision = _state.Revision; _pending = true; _canvas.CanEdit = false; Edited?.Invoke(message);
    }
    public void UpdateState(KiasControllerEditorState state)
    {
        _state = state; _pending = false;
        _updating = true; _enabled.Pressed = state.Enabled; _enabled.Disabled = !state.HasCard || !state.Online; _updating = false;
        _canvas.CanEdit = state.HasCard && state.Online && state.Editing;
        _write.Disabled = !state.HasCard || !state.Online || state.Errors.Count > 0;
        _discard.Disabled = !state.HasCard || !state.Dirty; _eject.Disabled = !state.HasCard || state.Dirty;
        _name.Text = state.Name;
        _status.Text = Loc.GetString("kias-controller-editor-status", ("card", Loc.GetString(state.HasCard ? "kias-controller-present" : "kias-controller-absent")),
            ("online", Loc.GetString(state.Online ? "kias-status-online" : "kias-status-offline")),
            ("dirty", Loc.GetString(state.Dirty ? "kias-controller-unsaved" : "kias-controller-saved")),
            ("nodes", state.Nodes.Count), ("wires", state.Wires.Count)) + (state.Errors.Count == 0 ? string.Empty : "\n" + string.Join(", ", state.Errors.Select(KiasControllerLabels.Error)));
        var selectedPreset = _presets.SelectedId; var selectedLegacy = _legacy.SelectedId;
        _presets.Clear(); _legacy.Clear();
        for (var i = 0; i < state.Presets.Count; i++) _presets.AddItem(Loc.TryGetString($"kias-preset-{state.Presets[i]}", out var presetTitle) ? presetTitle : state.Presets[i], i);
        if (state.Presets.Count > 0) _presets.SelectId(Math.Clamp(selectedPreset, 0, state.Presets.Count - 1));
        for (var i = 0; i < state.Legacy.Count; i++) _legacy.AddItem(Loc.TryGetString($"kias-preset-{state.Legacy[i]}", out var legacyTitle) ? legacyTitle : state.Legacy[i], i);
        if (state.Legacy.Count > 0) _legacy.SelectId(Math.Clamp(selectedLegacy, 0, state.Legacy.Count - 1));
        RebuildPalette(); _canvas.SetState(state);
    }
    private void PaletteItem(string title, KiasNodeKind kind, string profile = "", NetEntity? binding = null)
    {
        if (_search.Text.Length > 0 && !title.Contains(_search.Text, StringComparison.OrdinalIgnoreCase)) return;
        var button = new Button
        {
            Text = title,
            TextAlign = Label.AlignMode.Left,
            ClipText = true,
            ToolTip = title,
            Disabled = !_state.HasCard || !_state.Online
        };
        button.Label.RemoveStyleClass(ContainerButton.StyleClassButton);
        button.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick) return;
            var pixel = args.PointerLocation.Position - _canvas.GlobalPixelPosition;
            var position = pixel.X >= 0 && pixel.Y >= 0 && pixel.X <= _canvas.PixelSize.X && pixel.Y <= _canvas.PixelSize.Y
                ? _canvas.GraphPosition(pixel) : _canvas.Center;
            Send(new() { Edit = KiasGraphEdit.Add, Kind = kind, Profile = profile, Binding = binding, X = position.X, Y = position.Y });
        };
        _palette.AddChild(button);
    }
    private void RebuildPalette()
    {
        _palette.RemoveAllChildren();
        foreach (var category in new[] { "logic", "values", "state" })
        {
            _palette.AddChild(new Label { Text = Loc.GetString($"kias-controller-{category}") });
            foreach (var kind in Enum.GetValues<KiasNodeKind>())
            {
                if (KiasGraphCatalog.External(kind)) continue;
                var group = kind is KiasNodeKind.BoolConstant or KiasNodeKind.NumberConstant or KiasNodeKind.StringConstant or KiasNodeKind.EnumConstant
                    ? "values" : kind is KiasNodeKind.Timer or KiasNodeKind.Clock or KiasNodeKind.Latch or KiasNodeKind.StringLatch
                        or KiasNodeKind.Toggle or KiasNodeKind.Counter or KiasNodeKind.Edge or KiasNodeKind.Cooldown ? "state" : "logic";
                if (category == group) PaletteItem(Loc.GetString($"kias-controller-node-{kind.ToString().ToLowerInvariant()}"), kind);
            }
        }
        foreach (var kind in new[] { KiasNodeKind.Any, KiasNodeKind.All })
        {
            _palette.AddChild(new Label { Text = Loc.GetString(kind == KiasNodeKind.Any ? "kias-controller-any" : "kias-controller-all") });
            foreach (var profile in _state.Profiles)
                PaletteItem($"{kind.ToString().ToUpperInvariant()} {KiasControllerLabels.Profile(profile.Id, profile.Ports)}", kind, profile.Id);
        }
        _palette.AddChild(new Label { Text = Loc.GetString("kias-controller-devices") });
        foreach (var device in _state.Devices)
            PaletteItem($"{device.Name} ({KiasControllerLabels.Profile(device.Profile, _state.Profiles.First(p => p.Id == device.Profile).Ports)})",
                KiasNodeKind.Specific, device.Profile, device.Entity);
    }
    private LineEdit Field(string key, string text)
    {
        _settings.AddChild(new Label { Text = Loc.GetString(key) });
        var field = new LineEdit { Text = text }; _settings.AddChild(field); return field;
    }
    private void SelectNode(KiasGraphNodeView? node)
    {
        _settings.RemoveAllChildren(); if (node == null) return;
        _settings.AddChild(new Label { Text = $"#{node.Id}: {Loc.GetString($"kias-controller-node-{node.Kind.ToString().ToLowerInvariant()}")}" });
        if (KiasGraphCatalog.External(node.Kind)) _settings.AddChild(new Label { Text = $"{KiasControllerLabels.Profile(node.Profile, node.Ports)}\n{node.DeviceName}\n{Loc.GetString("kias-controller-matches")}: {node.Matched}" });
        var room = Field("kias-mode-room", node.Room); var group = Field("kias-mode-group", node.Group);
        var text = Field("kias-controller-text", node.Config.Text);
        var number = Field("kias-controller-number", node.Config.Number.ToString(CultureInfo.InvariantCulture));
        var seconds = Field("kias-controller-seconds", node.Config.Seconds.ToString(CultureInfo.InvariantCulture));
        var enumValue = Field("kias-controller-enum", node.Config.Enum.ToString(CultureInfo.InvariantCulture));
        var boolean = new CheckBox { Text = Loc.GetString("kias-controller-bool"), Pressed = node.Config.Bool }; _settings.AddChild(boolean);
        var comparison = new OptionButton();
        foreach (var item in Enum.GetValues<KiasComparison>()) comparison.AddItem(Loc.GetString($"kias-controller-comparison-{item.ToString().ToLowerInvariant()}"), (int) item);
        comparison.SelectId((int) node.Config.Comparison); comparison.OnItemSelected += args => comparison.SelectId(args.Id); _settings.AddChild(comparison);
        _settings.AddChild(Button("kias-save", () =>
        {
            if (!double.TryParse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric)
                || !double.TryParse(seconds.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var delay)
                || !int.TryParse(enumValue.Text, out var enumeration)) { _status.Text = Loc.GetString("kias-controller-invalid-number"); return; }
            Send(new() { Edit = KiasGraphEdit.Configure, Node = node.Id, Room = room.Text, Group = group.Text,
                Config = new() { Text = text.Text, Number = numeric, Seconds = delay, Enum = enumeration, Bool = boolean.Pressed,
                    Comparison = (KiasComparison) comparison.SelectedId } });
        }));
        _settings.AddChild(Button("kias-controller-remove-node", () => Send(new() { Edit = KiasGraphEdit.Remove, Node = node.Id })));
        foreach (var port in node.Ports)
            _settings.AddChild(new Label { Text = $"{port.Direction} {port.Id}: {port.Type}" });
    }
}

public sealed class KiasControllerRackWindow : FancyWindow
{
    public event Action<KiasControllerRackMessage>? Changed;
    private readonly BoxContainer _rows = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    public KiasControllerRackWindow()
    {
        Title = Loc.GetString("kias-controller-rack-title"); SetSize = new Vector2(650, 420); Resizable = true;
        XamlChildren.Add(_rows);
    }
    public void UpdateState(KiasControllerRackState state)
    {
        _rows.RemoveAllChildren();
        _rows.AddChild(new Label { Text = Loc.GetString("kias-controller-rack-status", ("online", Loc.GetString(state.Online ? "kias-status-online" : "kias-status-offline")), ("running", state.Running), ("load", state.Load)) });
        for (var i = 0; i < state.Slots.Count; i++)
        {
            var slot = i; var item = state.Slots[i]; var row = new BoxContainer(); _rows.AddChild(row);
            row.AddChild(new Label { HorizontalExpand = true, Text = $"{i + 1}. {item.Name} {(Loc.TryGetString($"kias-controller-status-{item.Status.ToLowerInvariant()}", out var status) ? status : item.Status)} {KiasControllerLabels.Error(item.Fault)}" });
            var toggle = new Button { Text = item.Enabled ? "OFF" : "ON", Disabled = !item.Inserted };
            toggle.OnPressed += _ => Changed?.Invoke(new() { Slot = slot, Enabled = !item.Enabled }); row.AddChild(toggle);
            var eject = new Button { Text = Loc.GetString("kias-controller-eject"), Disabled = !item.Inserted };
            eject.OnPressed += _ => Changed?.Invoke(new() { Slot = slot, Eject = true }); row.AddChild(eject);
        }
        var refresh = new Button { Text = Loc.GetString("kias-refresh") }; refresh.OnPressed += _ => Changed?.Invoke(new() { Refresh = true }); _rows.AddChild(refresh);
    }
}
