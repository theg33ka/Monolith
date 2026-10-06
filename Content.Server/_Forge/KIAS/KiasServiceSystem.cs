using System.Linq;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._Forge.KIAS;
using Content.Shared.DeviceLinking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Forge.KIAS;

public sealed class KiasServiceSystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private DeviceLinkSystem _links = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private KiasDisplaySystem _display = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private Robust.Server.GameObjects.UserInterfaceSystem _ui = default!;
    private readonly Dictionary<EntityUid, (TimeSpan Until, bool Previous)> _tests = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<KiasServiceToolComponent, AfterInteractEvent>(OnUse);
        SubscribeLocalEvent<KiasServiceToolComponent, GetVerbsEvent<AlternativeVerb>>(OnModes);
        SubscribeLocalEvent<KiasServiceToolComponent, KiasSetMessage>(OnMessage);
        SubscribeLocalEvent<KiasAvailabilityChangedEvent>(OnAvailability);
    }

    private void OnMessage(Entity<KiasServiceToolComponent> ent, ref KiasSetMessage args)
    {
        ent.Comp.Message = args.Message.Trim()[..Math.Min(args.Message.Trim().Length, 256)];
        _display.Refresh(ent);
    }

    private void OnModes(Entity<KiasServiceToolComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        foreach (var mode in Enum.GetValues<KiasServiceMode>())
        {
            var selected = mode;
            var tool = ent.Comp;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString($"kias-mode-{mode.ToString().ToLowerInvariant()}"),
                Act = () => { tool.Mode = selected; tool.Source = null; },
            });
        }
    }

    private void OnUse(Entity<KiasServiceToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (ent.Comp.Mode == KiasServiceMode.Group && Transform(target).GridUid is { } lightGrid
            && _kias.ActiveGrids.Contains(lightGrid))
        {
            var group = ent.Comp.Message.Trim().ToUpperInvariant();
            if (group.Length is < 1 or > 32)
                return;
            if (HasComp<Content.Shared.Light.Components.PoweredLightComponent>(target))
                EnsureComp<KiasLightGroupComponent>(target).Group = group;
            else if (TryComp<KiasLightControllerComponent>(target, out var controller) && _kias.IsOnline(target))
                controller.Group = group;
            else if (TryComp<KiasSpeakerComponent>(target, out var speakerGroup) && _kias.IsOnline(target))
                speakerGroup.Group = group;
            else
                return;
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("kias-light-group-set", ("group", group)), target, args.User);
            return;
        }
        if (!HasComp<KiasDeviceComponent>(target))
            return;
        args.Handled = true;
        if (ent.Comp.Mode == KiasServiceMode.Diagnose)
        {
            _popup.PopupEntity(Loc.GetString("kias-device-status", ("status", Loc.GetString($"kias-status-{Comp<KiasDeviceComponent>(target).Status.ToString().ToLowerInvariant()}"))), target, args.User);
            return;
        }
        if (!_kias.IsOnline(target) || Transform(target).GridUid is not { } grid)
        {
            _popup.PopupEntity(Loc.GetString("kias-status-offline"), target, args.User);
            return;
        }
        switch (ent.Comp.Mode)
        {
            case KiasServiceMode.Room:
                var room = ent.Comp.Message.Trim();
                Comp<KiasDeviceComponent>(target).Room = room[..Math.Min(room.Length, 64)];
                _popup.PopupEntity(Loc.GetString("kias-room-set", ("room", Comp<KiasDeviceComponent>(target).Room)), target, args.User);
                break;
            case KiasServiceMode.Link:
                if (HasComp<DeviceLinkSourceComponent>(target))
                {
                    ent.Comp.Source = target;
                    _popup.PopupEntity(Loc.GetString("kias-source-selected"), target, args.User);
                }
                else if (ent.Comp.Source is { } source && _kias.IsOnline(source)
                         && Transform(source).GridUid == grid && TryComp<KiasSpeakerComponent>(target, out var speaker))
                {
                    _links.LinkDefaults(args.User, source, target);
                    foreach (var (sourcePort, sinkPort) in _links.GetLinks(source, target))
                    {
                        if (sinkPort.ToString() != "KiasAnnounce")
                            continue;
                        speaker.Links.RemoveAll(link => link.Source == source && link.SourcePort == sourcePort.ToString());
                        speaker.Links.Add(new KiasSpeakerLink { Source = source, SourcePort = sourcePort, Message = ent.Comp.Message });
                    }
                    ent.Comp.Source = null;
                }
                break;
            case KiasServiceMode.Diagnose:
                _popup.PopupEntity(Loc.GetString("kias-device-status", ("status", Loc.GetString($"kias-status-{Comp<KiasDeviceComponent>(target).Status.ToString().ToLowerInvariant()}"))), target, args.User);
                break;
            case KiasServiceMode.Coverage:
                ent.Comp.Coverage = Coverage(grid, target);
                _ui.TryOpenUi(ent.Owner, KiasUiKey.Key, args.User);
                _display.Refresh(ent);
                break;
            case KiasServiceMode.Test:
                if (_timing.CurTime < ent.Comp.NextTest || _tests.ContainsKey(grid))
                {
                    _popup.PopupEntity(Loc.GetString("kias-test-cooldown"), target, args.User);
                    break;
                }
                ent.Comp.NextTest = _timing.CurTime + TimeSpan.FromSeconds(10);
                var runtime = Comp<KiasGridComponent>(grid);
                _tests[grid] = (_timing.CurTime + TimeSpan.FromSeconds(5), runtime.Testing);
                runtime.Testing = true;
                var announce = new KiasAnnouncementEvent(grid, Loc.GetString("kias-test-start"));
                RaiseLocalEvent(grid, ref announce, true);
                _popup.PopupEntity(string.Join("\n", runtime.Devices.Take(64).Select(uid =>
                    $"{Name(uid)}: {Loc.GetString($"kias-status-{Comp<KiasDeviceComponent>(uid).Status.ToString().ToLowerInvariant()}")}")), target, args.User);
                if (TryComp<DeviceLinkSourceComponent>(target, out var ports) && ports.Ports.Any(port => port.ToString() == "KiasMotion"))
                    _links.InvokePort(target, "KiasMotion");
                break;
        }
    }

    private List<byte> Coverage(EntityUid grid, EntityUid target)
    {
        var cells = new List<byte>();
        if (!TryComp<MapGridComponent>(grid, out var map) || !TryComp<KiasGridComponent>(grid, out var runtime) || runtime.Core is not { } core)
            return cells;
        var origin = _map.TileIndicesFor(grid, map, Transform(target).Coordinates);
        var coreTile = _map.TileIndicesFor(grid, map, Transform(core).Coordinates);
        var cables = runtime.Cables.Where(uid => !TerminatingOrDeleted(uid))
            .Select(uid => _map.TileIndicesFor(grid, map, Transform(uid).Coordinates)).ToHashSet();
        for (var y = 4; y >= -4; y--)
        for (var x = -4; x <= 4; x++)
        {
            var tile = origin + new Vector2i(x, y);
            var flags = runtime.Topology.Connected(coreTile, tile) ? 1 : 0;
            if (cables.Contains(tile)) flags |= 2;
            if (x == 0 && y == 0) flags |= 4;
            cells.Add((byte) flags);
        }
        return cells;
    }

    public override void Update(float frameTime)
    {
        if (_tests.Count == 0)
            return;
        foreach (var (grid, test) in _tests.ToArray())
        {
            if (TerminatingOrDeleted(grid))
                _tests.Remove(grid);
            else if (_timing.CurTime >= test.Until)
            {
                Comp<KiasGridComponent>(grid).Testing = test.Previous;
                _tests.Remove(grid);
                var announce = new KiasAnnouncementEvent(grid, Loc.GetString("kias-test-done"));
                RaiseLocalEvent(grid, ref announce, true);
            }
        }
    }

    private void OnAvailability(ref KiasAvailabilityChangedEvent args)
    {
        if (!args.Active && _tests.Remove(args.Grid, out var test) && TryComp<KiasGridComponent>(args.Grid, out var runtime))
            runtime.Testing = test.Previous;
    }
}
