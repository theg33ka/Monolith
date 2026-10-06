using System.Linq;
using System.Text;
using Content.Server.Chat.Systems;
using Content.Shared._Forge.KIAS;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceLinking;
using Content.Shared.Chat;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Forge.KIAS;

public sealed class KiasDisplaySystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;
    private readonly Dictionary<EntityUid, TimeSpan> _toneAfter = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<KiasTopologyChangedEvent>(OnTopology, after: new[] { typeof(KiasCrewSystem) });
        SubscribeLocalEvent<KiasAnnouncementEvent>(OnAnnouncement);
        SubscribeLocalEvent<KiasDisplayComponent, BoundUIOpenedEvent>(OnOpen);
        SubscribeLocalEvent<KiasDisplayComponent, KiasRefreshMessage>(OnRefresh);
        SubscribeLocalEvent<KiasSpeakerComponent, SignalReceivedEvent>(OnSignal);
        SubscribeLocalEvent<KiasSpeakerComponent, ComponentShutdown>(OnSpeakerShutdown);
        SubscribeLocalEvent<KiasCountsChangedEvent>(OnCounts);
        SubscribeLocalEvent<KiasAvailabilityChangedEvent>(OnAvailability);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoval);
    }

    private void OnTopology(ref KiasTopologyChangedEvent args)
    {
        if (!TryComp<KiasGridComponent>(args.Grid, out var runtime))
            return;
        foreach (var uid in runtime.Devices)
        {
            if (!_kias.IsOnline(uid) && TryComp<KiasSpeakerComponent>(uid, out var speaker))
                speaker.Tone = _audio.Stop(speaker.Tone);
        }
        if (!runtime.Devices.Any(uid => HasComp<KiasDisplayComponent>(uid) && _ui.IsUiOpen(uid, KiasUiKey.Key)))
            return;
        RefreshTargets(args.Grid, runtime);
        foreach (var uid in runtime.Devices)
        {
            if (HasComp<KiasDisplayComponent>(uid) && _ui.IsUiOpen(uid, KiasUiKey.Key))
                Refresh(uid);
        }
    }

    private void OnOpen(Entity<KiasDisplayComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (Transform(ent).GridUid is { } grid && TryComp<KiasGridComponent>(grid, out var runtime))
            RefreshTargets(grid, runtime);
        Refresh(ent);
    }
    private void OnCounts(ref KiasCountsChangedEvent args)
    {
        RefreshOpen(args.Grid);
    }
    private void OnRefresh(Entity<KiasDisplayComponent> ent, ref KiasRefreshMessage args) => Refresh(ent);

    public void Refresh(EntityUid display)
    {
        var state = new KiasUiState();
        if (TryComp<KiasServiceToolComponent>(display, out var tool))
        {
            state.ServiceTool = true;
            state.Message = tool.Message;
            state.Coverage = tool.Coverage;
        }
        if (Transform(display).GridUid is { } grid && TryComp<KiasGridComponent>(grid, out var runtime))
        {
            state.Online = _kias.IsOnline(display);
            state.Entities = runtime.Entities;
            state.Crew = runtime.Crew;
            state.Log = runtime.CrewDetails + "\n" + string.Join("\n", runtime.Log);
            state.CrewDetails = runtime.CrewDetails;
            state.Power = _kias.HasRole(grid, KiasDeviceRole.Power) ? runtime.PowerDetails : Loc.GetString("kias-power-unavailable");
            var atmos = new StringBuilder();
            var defence = new StringBuilder();
            var navigation = new StringBuilder();
            var faults = new StringBuilder();
            var devices = new StringBuilder();
            foreach (var uid in runtime.Devices.OrderBy(uid => uid.Id))
            {
                if (!TryComp<KiasDeviceComponent>(uid, out var device))
                    continue;
                devices.AppendLine($"{Name(uid)}: {Loc.GetString($"kias-status-{device.Status.ToString().ToLowerInvariant()}")}");
                var line = $"{Name(uid)}: {Loc.GetString($"kias-status-{device.Status.ToString().ToLowerInvariant()}")}";
                if (device.Role is KiasDeviceRole.Atmosphere or KiasDeviceRole.Suppression or KiasDeviceRole.Scanner)
                    atmos.AppendLine(line);
                if (device.Role is KiasDeviceRole.Defence or KiasDeviceRole.PdcRadar or KiasDeviceRole.PdcWeapon or KiasDeviceRole.Hull or KiasDeviceRole.WeaponFlash)
                    defence.AppendLine(line);
                if (device.Role is KiasDeviceRole.Navigation or KiasDeviceRole.Horizon or KiasDeviceRole.Proximity)
                    navigation.AppendLine(line);
                if (device.Status != KiasDeviceStatus.Online)
                    faults.AppendLine(line);
                if (TryComp<KiasPdcWeaponComponent>(uid, out var weapon) && weapon.AutomaticGrid != null)
                    defence.AppendLine(Loc.GetString("kias-pdc-reserved", ("weapon", Name(uid))));
                if (TryComp<KiasDefenceComponent>(uid, out var server))
                    defence.AppendLine(Loc.GetString(server.PdcEnabled ? "kias-pdc-enabled" : "kias-pdc-disabled"));
            }
            foreach (var uid in runtime.ProtocolTargets)
            {
                if (!TerminatingOrDeleted(uid) && Transform(uid).GridUid == grid)
                    state.Targets.Add(new KiasUiTarget { Entity = GetNetEntity(uid), Name = Name(uid) });
            }
            state.Devices = devices.ToString();
            state.Atmos = atmos.ToString();
            state.Defence = defence.ToString();
            state.Navigation = navigation.ToString();
            state.Faults = faults.Length == 0 ? Loc.GetString("kias-no-faults") : faults.ToString();
            if (runtime.Core is { } core && TryComp<KiasProtocolComponent>(core, out var protocols))
            {
                state.ProtocolsAvailable = state.Online && !state.ServiceTool;
                state.ProtocolRevision = protocols.Revision;
                state.Alert = Loc.GetString($"kias-alert-{protocols.Alert.ToString().ToLowerInvariant()}");
                foreach (var record in protocols.Protocols.Take(32))
                {
                    var action = record.Actions.FirstOrDefault() ?? new KiasProtocolAction();
                    state.Protocols.Add(new KiasProtocolView { Trigger = record.Trigger, Action = action.Kind,
                        Disposition = record.Disposition, MinimumValue = record.MinimumValue, RequireCrewUnavailable = record.RequireCrewUnavailable,
                        Target = action.Target is { } target && !TerminatingOrDeleted(target) ? GetNetEntity(target) : null,
                        Group = action.Group, Port = action.Port, Message = action.Message, Value = action.Value,
                        Enabled = record.Enabled, Cooldown = record.Cooldown });
                }
            }
        }
        if (TryComp<KiasRecorderComponent>(display, out var recorder))
            state.Log = string.Join("\n", recorder.Entries);
        _ui.SetUiState(display, KiasUiKey.Key, state);
    }

    public void RefreshOpen(EntityUid grid)
    {
        if (!TryComp<KiasGridComponent>(grid, out var runtime))
            return;
        foreach (var uid in runtime.Devices)
        {
            if (HasComp<KiasDisplayComponent>(uid) && _ui.IsUiOpen(uid, KiasUiKey.Key))
                Refresh(uid);
        }
    }

    private void RefreshTargets(EntityUid grid, KiasGridComponent runtime)
    {
        if (!TryComp<MapGridComponent>(grid, out var map))
            return;
        var candidates = new HashSet<EntityUid>();
        _lookup.GetLocalEntitiesIntersecting(grid, map.LocalAABB, candidates);
        runtime.ProtocolTargets.Clear();
        foreach (var uid in candidates.OrderBy(uid => uid.Id))
        {
            if (!TerminatingOrDeleted(uid) && Transform(uid).GridUid == grid && (HasComp<KiasDeviceComponent>(uid) || HasComp<DeviceLinkSinkComponent>(uid)))
                runtime.ProtocolTargets.Add(uid);
            if (runtime.ProtocolTargets.Count >= 128)
                break;
        }
    }

    private void OnAnnouncement(ref KiasAnnouncementEvent args)
    {
        if (!TryComp<KiasGridComponent>(args.Grid, out var runtime) || !runtime.Active || !runtime.Online.Any(HasComp<KiasSpeakerComponent>))
            return;
        Announce(args.Grid, args.Message, args.Warning, args.Speaker, args.Group);
    }

    private void OnAvailability(ref KiasAvailabilityChangedEvent args)
    {
        if (!args.Active)
        {
            _toneAfter.Remove(args.Grid);
            if (TryComp<KiasGridComponent>(args.Grid, out var runtime))
            {
                foreach (var uid in runtime.Devices)
                {
                    if (TryComp<KiasSpeakerComponent>(uid, out var speaker))
                        speaker.Tone = _audio.Stop(speaker.Tone);
                }
            }
        }
    }
    private void OnSpeakerShutdown(Entity<KiasSpeakerComponent> ent, ref ComponentShutdown args) => ent.Comp.Tone = _audio.Stop(ent.Comp.Tone);
    private void OnGridRemoval(GridRemovalEvent args) => _toneAfter.Remove(args.EntityUid);

    private void OnSignal(Entity<KiasSpeakerComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port != "KiasAnnounce" || !_kias.IsOnline(ent)
            || Transform(ent).GridUid is not { } grid)
            return;
        var trigger = args.Trigger;
        var sourcePort = args.SourcePort;
        if (trigger is { } source && (TerminatingOrDeleted(source) || Transform(source).GridUid != grid
            || HasComp<KiasDeviceComponent>(source) && !_kias.IsOnline(source)))
            return;
        var message = ent.Comp.Links.FirstOrDefault(link => link.Source == trigger && link.SourcePort == sourcePort)?.Message ?? ent.Comp.Message;
        if (!string.IsNullOrWhiteSpace(message))
            Announce(grid, message, selected: ent.Owner);
    }

    private void Announce(EntityUid grid, string message, bool warning = false, EntityUid? selected = null, string group = "")
    {
        var speakers = Comp<KiasGridComponent>(grid).Online.Where(uid => _kias.IsOnline(uid)
            && TryComp<KiasSpeakerComponent>(uid, out var speaker)
            && (selected == null || selected == uid)
            && (string.IsNullOrWhiteSpace(group) || group == "SHIP" || speaker.Group == group)).ToArray();
        if (speakers.Length == 0)
            return;
        var playTone = _toneAfter.GetValueOrDefault(grid) <= _timing.CurTime;
        if (playTone)
            _toneAfter[grid] = _timing.CurTime + TimeSpan.FromSeconds(3);
        foreach (var speaker in speakers)
        {
            _chat.TrySendInGameICMessage(speaker, message, InGameICChatType.Speak, hideChat: false,
                checkRadioPrefix: false, ignoreActionBlocker: true);
            if (playTone)
            {
                var component = Comp<KiasSpeakerComponent>(speaker);
                _audio.Stop(component.Tone);
                component.Tone = _audio.PlayPvs(new SoundPathSpecifier(warning ? "/Audio/Misc/redalert.ogg" : "/Audio/Effects/alert.ogg"), speaker,
                    AudioParams.Default.WithVolume(-4))?.Entity;
            }
        }
    }
}
