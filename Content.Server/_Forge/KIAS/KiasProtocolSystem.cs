using System.Linq;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Forge.KIAS;
using Content.Shared._Forge.KIAS.Controllers;
using Content.Server._Forge.KIAS.Controllers;
using Content.Shared.Atmos.Monitor;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Forge.KIAS;

public sealed class KiasProtocolSystem : EntitySystem
{
    public void SetAlert(EntityUid grid, KiasAlert alert)
    {
        if (TryProtocol(grid, out var protocols)) protocols.Alert = alert;
    }
    public void ResetAlert(EntityUid grid)
    {
        if (!TryProtocol(grid, out var protocols)) return;
        protocols.Alert = KiasAlert.Normal;
        protocols.MaydayReason = string.Empty;
        protocols.CriticalLatched = false;
        _shuttles.SetKiasMayday(grid, false);
        if (Comp<KiasGridComponent>(grid).Core is { } core)
            EntityManager.System<KiasControllerIoSystem>().Emit(core, "Automation", "AlertReset", KiasGraphValue.Pulse);
        _display.RefreshOpen(grid);
    }
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private KiasSafetySystem _safety = default!;
    [Dependency] private KiasCrewSystem _crew = default!;
    [Dependency] private KiasActuatorSystem _actuators = default!;
    [Dependency] private KiasRelaySystem _relays = default!;
    [Dependency] private KiasDefenceSystem _defence = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedShuttleSystem _shuttles = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private KiasDisplaySystem _display = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private KiasHullSystem _hull = default!;
    private readonly HashSet<EntityUid> _executing = new();
    private readonly KiasPeriodicScheduler _emergencies = new(1);
    private readonly HashSet<EntityUid> _fireAlarms = new();
    private readonly HashSet<EntityUid> _atmosAlarms = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<KiasHullImpactEvent>(OnImpact);
        SubscribeLocalEvent<KiasHullDamageEvent>(OnDamage);
        SubscribeLocalEvent<KiasGridCollisionEvent>(OnCollision);
        SubscribeLocalEvent<KiasAnomalyGrowthEvent>(OnAnomaly);
        SubscribeLocalEvent<KiasAtmosStateChangedEvent>(OnAtmos);
        SubscribeLocalEvent<KiasBluespaceDisturbanceEvent>(OnContact);
        SubscribeLocalEvent<KiasAutopilotArrivedEvent>(OnArrival);
        SubscribeLocalEvent<KiasWeaponFlashEvent>(OnFlash);
        SubscribeLocalEvent<KiasProximityEvent>(OnProximity);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<AtmosAlarmEvent>(OnFireAlarm);
        SubscribeLocalEvent<KiasPowerDeficitEvent>(OnPowerDeficit);
        SubscribeLocalEvent<KiasDisplayComponent, KiasProtocolMessage>(OnConfigure);
        SubscribeLocalEvent<KiasDisplayComponent, KiasControlMessage>(OnControl);
        SubscribeLocalEvent<KiasAvailabilityChangedEvent>(OnAvailability);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
        SubscribeLocalEvent<KiasManagementComponent, KiasRunProtocolMessage>(OnRunProtocol);
    }

    private void OnImpact(ref KiasHullImpactEvent args)
    {
        if (_hull.HasMonitor<KiasHullSensorComponent>(args.Grid, args.Structure))
            Trigger(args.Grid, KiasTrigger.HullImpact, message: Loc.GetString("kias-hull-impact", ("location", _safety.Location(args.Grid, args.Structure)), ("amount", 1)));
    }
    private void OnGridRemoved(GridRemovalEvent args)
    {
        _emergencies.Remove(args.EntityUid);
        ClearAlarmSources(args.EntityUid);
    }

    private void ClearAlarmSources(EntityUid grid)
    {
        _fireAlarms.RemoveWhere(uid => TerminatingOrDeleted(uid) || Transform(uid).GridUid == grid);
        _atmosAlarms.RemoveWhere(uid => TerminatingOrDeleted(uid) || Transform(uid).GridUid == grid);
    }

    private bool HasDistress(EntityUid grid, KiasProtocolComponent protocols) => protocols.DamageEvidenceUntil > _timing.CurTime
        || protocols.CrewDistressUntil > _timing.CurTime || EntityManager.System<KiasPowerSystem>().HasDeficit(grid)
        || _fireAlarms.Any(alarm => !TerminatingOrDeleted(alarm) && Transform(alarm).GridUid == grid);

    private void OnRunProtocol(Entity<KiasManagementComponent> ent, ref KiasRunProtocolMessage args)
    {
        if (!_kias.IsOnline(ent) || Transform(ent).GridUid is not { } grid || !_kias.CanConfigure(grid, args.Actor)
            || !TryProtocol(grid, out var config) || args.Index < 0 || args.Index >= config.Protocols.Count
            || config.Cooldowns.GetValueOrDefault(args.Index) > _timing.CurTime || !_executing.Add(grid)) return;
        try
        {
            var record = config.Protocols[args.Index];
            if (record.RequireCrewUnavailable && !_crew.CrewUnavailable(grid)) return;
            config.Cooldowns[args.Index] = _timing.CurTime + TimeSpan.FromSeconds(Math.Clamp(record.Cooldown, 1, 600));
            var fired = new KiasProtocolFiredEvent(grid, record.Trigger, args.Index, record.PresetId, $"manual:{args.Index}");
            RaiseLocalEvent(grid, ref fired, true);
            foreach (var action in record.Actions.Take(8).ToArray())
            {
                if (!_kias.ActiveGrids.Contains(grid)) break;
                Execute(grid, action, record.Trigger, null, $"manual:{args.Index}");
            }
        }
        finally { _executing.Remove(grid); _display.RefreshOpen(grid); }
    }

    private EntityUid Antenna(EntityUid grid) => TryComp<KiasGridComponent>(grid, out var runtime)
        ? runtime.Online.FirstOrDefault(uid => HasComp<KiasMaydayAntennaComponent>(uid) && _kias.IsOnline(uid)) : default;

    public bool MedicalHelp(EntityUid grid, string reason)
    {
        if (!TryProtocol(grid, out var protocols) || protocols.MedicalAfter > _timing.CurTime
            || protocols.CrewUnavailableSince == TimeSpan.Zero || _timing.CurTime - protocols.CrewUnavailableSince < TimeSpan.FromSeconds(30)
            || !_crew.CrewUnavailable(grid) || !HasDistress(grid, protocols) || Comp<KiasGridComponent>(grid).Testing) return false;
        var source = Antenna(grid);
        if (!source.Valid) return false;
        protocols.MedicalAfter = _timing.CurTime + TimeSpan.FromSeconds(180);
        var position = EntityManager.System<SharedTransformSystem>().GetMapCoordinates(grid);
        var message = Loc.GetString("kias-medical-help", ("ship", Name(grid)), ("x", MathF.Round(position.X)),
            ("y", MathF.Round(position.Y)), ("reason", reason));
        _radio.SendRadioMessage(source, message, "Medical", source);
        _safety.Publish(grid, message, true, announce: false, key: "medical-help");
        return true;
    }

    public override void Update(float frameTime)
    {
        using var measurement = new KiasUpdateMeasurement(_kias);
        for (var i = 0; i < 8 && _emergencies.TryDue(_timing.CurTime, out var grid); i++)
        {
            if (!TryProtocol(grid, out var protocols)) { _emergencies.Remove(grid); continue; }
            var distress = HasDistress(grid, protocols);
            if (distress && _crew.CrewUnavailable(grid))
            {
                if (protocols.CrewUnavailableSince == TimeSpan.Zero) protocols.CrewUnavailableSince = _timing.CurTime;
                if (_timing.CurTime - protocols.CrewUnavailableSince >= TimeSpan.FromSeconds(30))
                    Trigger(grid, KiasTrigger.CrewUnavailable);
            }
            else protocols.CrewUnavailableSince = TimeSpan.Zero;
            var critical = protocols.DamageEvidenceUntil > _timing.CurTime &&
                (EntityManager.System<KiasPowerSystem>().HasDeficit(grid) || protocols.CrewUnavailableSince != TimeSpan.Zero
                    && _timing.CurTime - protocols.CrewUnavailableSince >= TimeSpan.FromSeconds(30));
            if (critical && !protocols.CriticalLatched) Trigger(grid, KiasTrigger.VesselCritical);
            protocols.CriticalLatched = critical;
            if (protocols.MaydayReason.Length > 0)
            {
                _shuttles.SetKiasMayday(grid, Antenna(grid).Valid);
                if (protocols.MaydayAfter <= _timing.CurTime) Mayday(grid, protocols.MaydayReason);
            }
            if (!distress && protocols.MaydayReason.Length == 0) _emergencies.Remove(grid);
        }
    }

    private void OnPowerDeficit(ref KiasPowerDeficitEvent args)
    {
        Trigger(args.Grid, KiasTrigger.PowerDeficit, value: args.Consumption - args.Supply,
            message: Loc.GetString("kias-power-deficit", ("channel", Loc.GetString($"kias-relay-{args.Channel.ToString().ToLowerInvariant()}"))), eventKey: args.Channel.ToString());
        _emergencies.Add(args.Grid, _timing.CurTime);
    }
    private void OnCollision(ref KiasGridCollisionEvent args)
    {
        if (_hull.DetectsCollision(args.Grid, args.RelativeSpeed))
            Trigger(args.Grid, KiasTrigger.Collision, value: args.RelativeSpeed, eventKey: args.OtherGrid.ToString());
    }
    private void OnAnomaly(ref KiasAnomalyGrowthEvent args) => Trigger(args.Grid, KiasTrigger.AnomalyGrowth,
        message: Loc.GetString("kias-anomaly-growth", ("location", args.Location)), eventKey: args.Source.ToString());
    private void OnAtmos(ref KiasAtmosStateChangedEvent args)
    {
        if (args.State == AtmosAlarmType.Danger)
        {
            _atmosAlarms.Add(args.Source);
            Trigger(args.Grid, KiasTrigger.AtmosDanger, message: Loc.GetString("kias-atmos-danger", ("location", args.Location)), eventKey: args.Source.ToString());
        }
        else if (args.State == AtmosAlarmType.Normal && _atmosAlarms.Remove(args.Source))
            Trigger(args.Grid, KiasTrigger.AtmosClear, message: Loc.GetString("kias-atmos-clear", ("location", args.Location)), eventKey: args.Source.ToString());
    }
    private void OnContact(ref KiasBluespaceDisturbanceEvent args) => Trigger(args.Grid, KiasTrigger.Contact, args.Disposition, args.Distance,
        Loc.GetString("kias-contact", ("disposition", Loc.GetString($"kias-contact-{args.Disposition.ToString().ToLowerInvariant()}")),
            ("range", MathF.Round(args.Distance / 1000f, 1)), ("bearing", Math.Round(args.Bearing))), args.Contact.ToString());
    private void OnArrival(ref KiasAutopilotArrivedEvent args) => Trigger(args.Grid, KiasTrigger.Arrival, message: Loc.GetString("kias-autopilot-arrived"));
    private void OnFlash(ref KiasWeaponFlashEvent args) => Trigger(args.Grid, KiasTrigger.WeaponFlash, args.Disposition,
        message: Loc.GetString("kias-weapon-flash"), eventKey: args.Source.ToString());
    private void OnProximity(ref KiasProximityEvent args) => Trigger(args.Grid, KiasTrigger.Proximity, args.Disposition, args.Distance, eventKey: args.Contact.ToString());

    private void OnDamage(ref KiasHullDamageEvent args)
    {
        if (!_hull.HasMonitor<KiasIntegrityMonitorComponent>(args.Grid))
            return;
        Trigger(args.Grid, KiasTrigger.HullDamage, value: args.Damage, eventKey: args.Structure.ToString());
        if (!TryProtocol(args.Grid, out var protocols))
            return;
        if (_timing.CurTime > protocols.DamageWindow)
        {
            protocols.RecentDamage = 0;
            protocols.DamageWindow = _timing.CurTime + TimeSpan.FromSeconds(5);
        }
        var previous = protocols.RecentDamage;
        protocols.RecentDamage += args.Damage;
        if (protocols.RecentDamage >= protocols.CriticalDamageThreshold)
        {
            protocols.DamageEvidenceUntil = _timing.CurTime + TimeSpan.FromSeconds(60);
            _emergencies.Add(args.Grid, _timing.CurTime);
        }
    }

    private void OnMobState(MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Critical or MobState.Dead) || !_crew.IsKiasTrackedEntity(args.Target)
            || Transform(args.Target).GridUid is not { } grid || !_kias.HasRole(grid, KiasDeviceRole.Crew)
            || !_crew.HasCoverage(grid, args.Target, KiasScannerModules.Biometric))
            return;
        var dead = args.NewMobState == MobState.Dead;
        var ev = new KiasCrewDistressEvent(grid, args.Target, dead);
        RaiseLocalEvent(grid, ref ev, true);
        var message = Loc.GetString(dead ? "kias-crew-dead" : "kias-crew-critical", ("location", _safety.Location(grid, args.Target)));
        _safety.Publish(grid, message, true, announce: false, key: $"crew:{args.Target}:{dead}");
        Trigger(grid, dead ? KiasTrigger.CrewDead : KiasTrigger.CrewCritical, message: message, eventKey: args.Target.ToString());
        if (_crew.IsRegisteredPerson(grid, args.Target) && TryProtocol(grid, out var clinical)) clinical.CrewDistressUntil = _timing.CurTime + TimeSpan.FromSeconds(60);
        if (_crew.IsRegisteredPerson(grid, args.Target) && _crew.HasUnknownAlongside(grid, args.Target))
            Trigger(grid, KiasTrigger.Boarding, message: Loc.GetString("kias-boarding", ("location", _safety.Location(grid, args.Target))), eventKey: args.Target.ToString());
        _emergencies.Add(grid, _timing.CurTime);
    }

    private void OnFireAlarm(AtmosAlarmEvent args)
    {
        if (args.Source is not { } source || !HasComp<FireAlarmComponent>(source)
            || Transform(source).GridUid is not { } grid || !_kias.HasRole(grid, KiasDeviceRole.Atmosphere)
            || !HasComp<KiasIntegratedComponent>(source) || !EntityManager.System<KiasIntegrationSystem>().CanControl(source))
            return;
        if (args.AlarmType != AtmosAlarmType.Danger)
        {
            if (args.AlarmType == AtmosAlarmType.Normal && _fireAlarms.Remove(source))
                Trigger(grid, KiasTrigger.FireClear, message: Loc.GetString("kias-fire-clear", ("location", _safety.Location(grid, source))), eventKey: source.ToString());
            return;
        }
        _fireAlarms.Add(source);
        _emergencies.Add(grid, _timing.CurTime);
        var ev = new KiasFireDetectedEvent(grid, source);
        RaiseLocalEvent(grid, ref ev, true);
        var message = Loc.GetString("kias-fire-alarm", ("location", _safety.Location(grid, source)));
        _safety.Publish(grid, message, true, announce: false, key: $"fire:{source}");
        Trigger(grid, KiasTrigger.Fire, message: message, eventKey: source.ToString());
    }

    private bool TryProtocol(EntityUid grid, out KiasProtocolComponent protocols)
    {
        protocols = default!;
        if (_kias.ActiveGrids.Contains(grid) && TryComp<KiasGridComponent>(grid, out var runtime)
            && runtime.Core is { } core && TryComp<KiasProtocolComponent>(core, out var found))
        {
            protocols = found;
            return true;
        }
        return false;
    }

    public void Trigger(EntityUid grid, KiasTrigger trigger, KiasContactDisposition? disposition = null, float value = 0, string? message = null, string? eventKey = null)
    {
        if (!TryProtocol(grid, out var protocols) || !_executing.Add(grid))
            return;
        try
        {
            if (Comp<KiasGridComponent>(grid).Core is { } source)
            {
                var controllers = EntityManager.System<KiasControllerIoSystem>();
                controllers.Emit(source, "Automation", "Message", KiasGraphValue.String(message ?? Loc.GetString($"kias-trigger-{trigger.ToString().ToLowerInvariant()}")));
                controllers.Emit(source, "Automation", "Value", KiasGraphValue.Numeric(value));
                controllers.Emit(source, "Automation", "Disposition", KiasGraphValue.Enumeration(disposition is { } contact ? (int) contact : -1));
                controllers.Emit(source, "Automation", "CrewUnavailable", KiasGraphValue.Boolean(_crew.CrewUnavailable(grid)));
                controllers.Emit(source, "Automation", "EventKey", KiasGraphValue.String(eventKey ?? string.Empty));
                controllers.Emit(source, "Automation", trigger.ToString(), KiasGraphValue.Pulse);
            }
            if (trigger is KiasTrigger.HullImpact or KiasTrigger.Collision or KiasTrigger.Manual || trigger == KiasTrigger.WeaponFlash && disposition == KiasContactDisposition.Hostile)
                protocols.Alert = (KiasAlert) Math.Max((int) protocols.Alert, (int) KiasAlert.Battle);
            else if (trigger is KiasTrigger.CrewCritical or KiasTrigger.CrewDead or KiasTrigger.VesselCritical or KiasTrigger.AtmosDanger or KiasTrigger.Fire or KiasTrigger.Boarding)
                protocols.Alert = (KiasAlert) Math.Max((int) protocols.Alert, (int) KiasAlert.Emergency);
            else if (trigger == KiasTrigger.Contact && protocols.Alert == KiasAlert.Normal)
                protocols.Alert = KiasAlert.Contact;
            foreach (var (record, index) in protocols.Protocols.Take(32).Select((p, i) => (p, i)))
            {
                var cooldown = eventKey == null ? protocols.Cooldowns.GetValueOrDefault(index)
                    : protocols.EventCooldowns.GetValueOrDefault((index, eventKey));
                if (!record.Enabled || record.Trigger != trigger || record.Disposition != null && record.Disposition != disposition
                    || value < record.MinimumValue || cooldown > _timing.CurTime)
                    continue;
                if (record.RequireCrewUnavailable && (trigger is not (KiasTrigger.CrewCritical or KiasTrigger.CrewDead
                        or KiasTrigger.VesselCritical or KiasTrigger.AtmosDanger or KiasTrigger.Fire or KiasTrigger.PowerDeficit or KiasTrigger.CrewUnavailable)
                    || !_crew.CrewUnavailable(grid)))
                    continue;
                var due = _timing.CurTime + TimeSpan.FromSeconds(Math.Clamp(record.Cooldown, 1, 600));
                if (eventKey == null) protocols.Cooldowns[index] = due;
                else
                {
                    if (protocols.EventCooldowns.Count >= 512)
                        foreach (var key in protocols.EventCooldowns.Where(pair => pair.Value <= _timing.CurTime).Select(pair => pair.Key).ToArray()) protocols.EventCooldowns.Remove(key);
                    if (protocols.EventCooldowns.Count < 512) protocols.EventCooldowns[(index, eventKey)] = due;
                }
                var fired = new KiasProtocolFiredEvent(grid, trigger, index, record.PresetId, eventKey ?? "—");
                RaiseLocalEvent(grid, ref fired, true);
                foreach (var action in record.Actions.Take(8).ToArray())
                {
                    if (!_kias.ActiveGrids.Contains(grid))
                        break;
                    Execute(grid, action, trigger, message, eventKey);
                }
            }
        }
        finally
        {
            _executing.Remove(grid);
            _display.RefreshOpen(grid);
        }
    }

    private void Execute(EntityUid grid, KiasProtocolAction action, KiasTrigger trigger, string? notification, string? eventKey)
    {
        var message = string.IsNullOrWhiteSpace(action.Message) ? notification ?? Loc.GetString($"kias-trigger-{trigger.ToString().ToLowerInvariant()}") : action.Message[..Math.Min(action.Message.Length, 256)];
        if (action.Kind == KiasActionKind.Announce || action.Kind == KiasActionKind.Record)
        {
            if (action.Target is { } speaker && (TerminatingOrDeleted(speaker) || Transform(speaker).GridUid != grid))
                return;
            _safety.Publish(grid, message, true, announce: action.Kind == KiasActionKind.Announce, speaker: action.Target,
                group: action.Group, key: $"protocol:{trigger}:{eventKey}:{action.Target}:{action.Group}:{action.Message}",
                channel: Channel(trigger), record: action.Kind == KiasActionKind.Record);
            return;
        }
        if (Comp<KiasGridComponent>(grid).Testing)
            return;
        if (action.Kind == KiasActionKind.MedicalHelp)
        {
            MedicalHelp(grid, message);
            return;
        }
        if (action.Kind == KiasActionKind.Mayday)
        {
            Mayday(grid, message);
            return;
        }
        if (action.Kind == KiasActionKind.Lights)
        {
            var ev = new KiasSetLightGroupEvent(grid, action.Group, action.Value);
            RaiseLocalEvent(grid, ref ev, true);
            return;
        }
        if (action.Kind == KiasActionKind.Pdc && action.Target == null)
        {
            foreach (var server in Comp<KiasGridComponent>(grid).Online.ToArray())
            {
                if (HasComp<KiasDefenceComponent>(server))
                    _defence.SetAutomatic(server, action.Value);
            }
            return;
        }
        if (action.Kind == KiasActionKind.FireLock)
        {
            foreach (var server in Comp<KiasGridComponent>(grid).Online)
                if (_kias.IsOnline(server) && TryComp<KiasDefenceComponent>(server, out var defence)) defence.FireLock = action.Value;
            return;
        }
        if (action.Target is not { } target || TerminatingOrDeleted(target) || Transform(target).GridUid != grid)
            return;
        if (HasComp<KiasDeviceComponent>(target) && !_kias.IsOnline(target)
            && !(HasComp<KiasIntegratedComponent>(target) && EntityManager.System<KiasIntegrationSystem>().CanControl(target)))
            return;
        switch (action.Kind)
        {
            case KiasActionKind.Suppression:
                _actuators.Suppress(target);
                break;
            case KiasActionKind.Relay:
                _relays.SetClosed(target, action.Value);
                break;
            case KiasActionKind.Pdc:
                _defence.SetAutomatic(target, action.Value);
                break;
            case KiasActionKind.Jammer:
                if (_kias.HasRole(grid, KiasDeviceRole.Defence))
                    EntityManager.System<JammerSystem>().SetEnabled(target, action.Value);
                break;
            case KiasActionKind.Decoy:
                if (_kias.HasRole(grid, KiasDeviceRole.Defence))
                    EntityManager.System<KiasCountermeasureSystem>().Deploy(target);
                break;
            case KiasActionKind.RestoreVentilation:
                if (_kias.HasRole(grid, KiasDeviceRole.Atmosphere))
                    EntityManager.System<KiasVentilationSystem>().Restore(target);
                break;
            case KiasActionKind.DevicePort:
                if (TryComp<DeviceLinkSinkComponent>(target, out var sink) && sink.Ports.Any(p => p.ToString() == action.Port))
                {
                    var ev = new SignalReceivedEvent(action.Port, Comp<KiasGridComponent>(grid).Core);
                    RaiseLocalEvent(target, ref ev);
                }
                break;
        }
    }

    private static KiasAudioChannel Channel(KiasTrigger trigger) => trigger switch
    {
        KiasTrigger.HullImpact or KiasTrigger.Collision or KiasTrigger.WeaponFlash or KiasTrigger.Manual => KiasAudioChannel.Battle,
        KiasTrigger.CrewCritical or KiasTrigger.CrewDead or KiasTrigger.VesselCritical or KiasTrigger.AtmosDanger or KiasTrigger.Fire => KiasAudioChannel.Emergency,
        KiasTrigger.HullDamage or KiasTrigger.AnomalyGrowth or KiasTrigger.PowerDeficit or KiasTrigger.Proximity => KiasAudioChannel.Warning,
        _ => KiasAudioChannel.Notification,
    };

    public bool Mayday(EntityUid grid, string reason)
    {
        if (!_kias.HasRole(grid, KiasDeviceRole.Navigation) || !TryProtocol(grid, out var protocols)
            || protocols.MaydayAfter > _timing.CurTime)
            return false;
        var runtime = Comp<KiasGridComponent>(grid);
        if (runtime.Testing)
            return false;
        var source = Antenna(grid);
        if (!source.Valid)
            return false;
        protocols.MaydayReason = reason[..Math.Min(reason.Length, 256)];
        protocols.MaydayAfter = _timing.CurTime + TimeSpan.FromSeconds(180);
        _emergencies.Add(grid, _timing.CurTime);
        var message = Loc.GetString("kias-mayday", ("ship", Name(grid)), ("reason", reason[..Math.Min(reason.Length, 256)]));
        _radio.SendRadioMessage(source, message, "Traffic", source);
        _shuttles.SetKiasMayday(grid, true);
        _safety.Publish(grid, message, true);
        return true;
    }

    private void OnAvailability(ref KiasAvailabilityChangedEvent args)
    {
        if (!args.Active)
        {
            _emergencies.Remove(args.Grid);
            ClearAlarmSources(args.Grid);
            _shuttles.SetKiasMayday(args.Grid, false);
            if (!TryComp<KiasGridComponent>(args.Grid, out var runtime))
                return;
            foreach (var uid in runtime.Devices)
            {
                if (!TryComp<KiasProtocolComponent>(uid, out var protocols))
                    continue;
                protocols.Cooldowns.Clear();
                protocols.EventCooldowns.Clear();
                protocols.RecentDamage = 0;
                protocols.DamageWindow = TimeSpan.Zero;
                protocols.Alert = KiasAlert.Normal;
                protocols.MaydayReason = string.Empty;
                protocols.CrewUnavailableSince = TimeSpan.Zero;
                protocols.DamageEvidenceUntil = TimeSpan.Zero;
                protocols.CrewDistressUntil = TimeSpan.Zero;
                protocols.CriticalLatched = false;
            }
        }
    }

    private void OnControl(Entity<KiasDisplayComponent> ent, ref KiasControlMessage args)
    {
        if (!HasComp<KiasManagementComponent>(ent) || Transform(ent).GridUid is not { } grid || !_kias.IsOnline(ent) || !_kias.CanConfigure(grid, args.Actor)
            || !TryProtocol(grid, out var protocols))
            return;
        if (args.Reset)
        {
            protocols.Alert = KiasAlert.Normal;
            protocols.MaydayReason = string.Empty;
            protocols.CriticalLatched = false;
            _shuttles.SetKiasMayday(grid, false);
            _display.RefreshOpen(grid);
        }
        else
            Trigger(grid, KiasTrigger.Manual);
    }

    private void OnConfigure(Entity<KiasDisplayComponent> ent, ref KiasProtocolMessage args)
    {
        if (!HasComp<KiasManagementComponent>(ent) || Transform(ent).GridUid is not { } grid || !_kias.IsOnline(ent) || !_kias.CanConfigure(grid, args.Actor)
            || !TryProtocol(grid, out var protocols))
        {
            _popup.PopupEntity(Loc.GetString("kias-config-owner"), ent, args.Actor);
            return;
        }
        if (args.Index < 0 || args.Index > protocols.Protocols.Count || args.Index >= 32)
            return;
        if (args.Delete)
        {
            if (args.Index < protocols.Protocols.Count)
                protocols.Protocols.RemoveAt(args.Index);
            protocols.Cooldowns.Clear();
            protocols.EventCooldowns.Clear();
            protocols.Revision++;
            _display.Refresh(ent);
            return;
        }
        if (!Enum.IsDefined(args.Trigger) || !Enum.IsDefined(args.Action) || !float.IsFinite(args.Cooldown)
            || !float.IsFinite(args.MinimumValue) || args.Disposition is { } disposition && !Enum.IsDefined(disposition))
            return;
        var record = new KiasProtocolRecord { Trigger = args.Trigger, Enabled = args.Enabled, Cooldown = Math.Clamp(args.Cooldown, 1, 600),
            Disposition = args.Disposition, MinimumValue = Math.Clamp(args.MinimumValue, 0, 1000000), RequireCrewUnavailable = args.RequireCrewUnavailable };
        var actions = args.Actions ?? new List<KiasProtocolActionView> { new() { Kind = args.Action, Target = args.Target,
            Value = args.Value, Group = args.Group, Port = args.Port, Message = args.Message } };
        if (actions.Count > 8) return;
        foreach (var action in actions)
        {
            if (!Enum.IsDefined(action.Kind) || action.Group.Length > 32 || action.Port.Length > 64 || action.Message.Length > 256) return;
            EntityUid? target = null;
            if (action.Target is { } net)
            {
                if (!TryGetEntity(net, out target) || target is not { } uid || TerminatingOrDeleted(uid) || Transform(uid).GridUid != grid) return;
            }
            record.Actions.Add(new KiasProtocolAction { Kind = action.Kind, Target = target, Value = action.Value,
                Group = action.Group.Trim(), Port = action.Port.Trim(), Message = action.Message.Trim() });
        }
        if (args.Index == protocols.Protocols.Count)
            protocols.Protocols.Add(record);
        else
        {
            record.PresetId = protocols.Protocols[args.Index].PresetId;
            if (args.Actions == null) record.Actions.AddRange(protocols.Protocols[args.Index].Actions.Skip(1).Take(7));
            protocols.Protocols[args.Index] = record;
        }
        protocols.Cooldowns.Remove(args.Index);
        protocols.EventCooldowns.Clear();
        protocols.Revision++;
        _display.Refresh(ent);
    }
}
