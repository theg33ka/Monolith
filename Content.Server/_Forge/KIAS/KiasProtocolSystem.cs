using System.Linq;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Forge.KIAS;
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
    }

    private void OnImpact(ref KiasHullImpactEvent args)
    {
        if (_hull.HasMonitor<KiasHullSensorComponent>(args.Grid))
            Trigger(args.Grid, KiasTrigger.HullImpact);
    }
    private void OnPowerDeficit(ref KiasPowerDeficitEvent args) => Trigger(args.Grid, KiasTrigger.PowerDeficit, value: args.Consumption - args.Supply);
    private void OnCollision(ref KiasGridCollisionEvent args)
    {
        if (_hull.DetectsCollision(args.Grid, args.RelativeSpeed))
            Trigger(args.Grid, KiasTrigger.Collision, value: args.RelativeSpeed);
    }
    private void OnAnomaly(ref KiasAnomalyGrowthEvent args) => Trigger(args.Grid, KiasTrigger.AnomalyGrowth);
    private void OnAtmos(ref KiasAtmosStateChangedEvent args)
    {
        if (args.State == AtmosAlarmType.Danger)
            Trigger(args.Grid, KiasTrigger.AtmosDanger);
    }
    private void OnContact(ref KiasBluespaceDisturbanceEvent args) => Trigger(args.Grid, KiasTrigger.Contact, args.Disposition, args.Distance);
    private void OnArrival(ref KiasAutopilotArrivedEvent args) => Trigger(args.Grid, KiasTrigger.Arrival);
    private void OnFlash(ref KiasWeaponFlashEvent args) => Trigger(args.Grid, KiasTrigger.WeaponFlash, args.Disposition);
    private void OnProximity(ref KiasProximityEvent args) => Trigger(args.Grid, KiasTrigger.Proximity, args.Disposition, args.Distance);

    private void OnDamage(ref KiasHullDamageEvent args)
    {
        if (!_hull.HasMonitor<KiasIntegrityMonitorComponent>(args.Grid))
            return;
        Trigger(args.Grid, KiasTrigger.HullDamage, value: args.Damage);
        if (!TryProtocol(args.Grid, out var protocols))
            return;
        if (_timing.CurTime > protocols.DamageWindow)
        {
            protocols.RecentDamage = 0;
            protocols.DamageWindow = _timing.CurTime + TimeSpan.FromSeconds(5);
        }
        var previous = protocols.RecentDamage;
        protocols.RecentDamage += args.Damage;
        if (previous < protocols.CriticalDamageThreshold && protocols.RecentDamage >= protocols.CriticalDamageThreshold)
            Trigger(args.Grid, KiasTrigger.VesselCritical, value: protocols.RecentDamage);
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
        _safety.Publish(grid, Loc.GetString(dead ? "kias-crew-dead" : "kias-crew-critical", ("location", _safety.Location(grid, args.Target))), true);
        Trigger(grid, dead ? KiasTrigger.CrewDead : KiasTrigger.CrewCritical);
    }

    private void OnFireAlarm(AtmosAlarmEvent args)
    {
        if (args.AlarmType != AtmosAlarmType.Danger || args.Source is not { } source || !HasComp<FireAlarmComponent>(source)
            || Transform(source).GridUid is not { } grid || !_kias.HasRole(grid, KiasDeviceRole.Atmosphere))
            return;
        var ev = new KiasFireDetectedEvent(grid, source);
        RaiseLocalEvent(grid, ref ev, true);
        _safety.Publish(grid, Loc.GetString("kias-fire-alarm", ("location", _safety.Location(grid, source))), true);
        Trigger(grid, KiasTrigger.Fire);
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

    public void Trigger(EntityUid grid, KiasTrigger trigger, KiasContactDisposition? disposition = null, float value = 0)
    {
        if (!TryProtocol(grid, out var protocols) || !_executing.Add(grid))
            return;
        try
        {
            if (trigger is KiasTrigger.HullImpact or KiasTrigger.Collision or KiasTrigger.Manual || trigger == KiasTrigger.WeaponFlash && disposition == KiasContactDisposition.Hostile)
                protocols.Alert = KiasAlert.Battle;
            else if (trigger is KiasTrigger.CrewCritical or KiasTrigger.CrewDead or KiasTrigger.VesselCritical or KiasTrigger.AtmosDanger or KiasTrigger.Fire)
                protocols.Alert = KiasAlert.Emergency;
            else if (trigger == KiasTrigger.Contact && protocols.Alert == KiasAlert.Normal)
                protocols.Alert = KiasAlert.Contact;
            foreach (var (record, index) in protocols.Protocols.Take(32).Select((p, i) => (p, i)))
            {
                if (!record.Enabled || record.Trigger != trigger || record.Disposition != null && record.Disposition != disposition
                    || value < record.MinimumValue || protocols.Cooldowns.GetValueOrDefault(index) > _timing.CurTime)
                    continue;
                if (record.RequireCrewUnavailable && (trigger is not (KiasTrigger.CrewCritical or KiasTrigger.CrewDead
                        or KiasTrigger.VesselCritical or KiasTrigger.AtmosDanger or KiasTrigger.Fire or KiasTrigger.PowerDeficit)
                    || !_crew.CrewUnavailable(grid)))
                    continue;
                protocols.Cooldowns[index] = _timing.CurTime + TimeSpan.FromSeconds(Math.Clamp(record.Cooldown, 1, 600));
                foreach (var action in record.Actions.Take(8).ToArray())
                {
                    if (!_kias.ActiveGrids.Contains(grid))
                        break;
                    Execute(grid, action, trigger);
                }
            }
        }
        finally
        {
            _executing.Remove(grid);
            _display.RefreshOpen(grid);
        }
    }

    private void Execute(EntityUid grid, KiasProtocolAction action, KiasTrigger trigger)
    {
        var message = string.IsNullOrWhiteSpace(action.Message) ? Loc.GetString($"kias-trigger-{trigger.ToString().ToLowerInvariant()}") : action.Message[..Math.Min(action.Message.Length, 256)];
        if (action.Kind == KiasActionKind.Announce || action.Kind == KiasActionKind.Record)
        {
            if (action.Target is { } speaker && (TerminatingOrDeleted(speaker) || Transform(speaker).GridUid != grid))
                return;
            _safety.Publish(grid, message, true, announce: action.Kind == KiasActionKind.Announce, speaker: action.Target, group: action.Group);
            return;
        }
        if (Comp<KiasGridComponent>(grid).Testing)
            return;
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
        if (action.Target is not { } target || TerminatingOrDeleted(target) || Transform(target).GridUid != grid)
            return;
        if (HasComp<KiasDeviceComponent>(target) && !_kias.IsOnline(target))
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
            case KiasActionKind.DevicePort:
                if (TryComp<DeviceLinkSinkComponent>(target, out var sink) && sink.Ports.Any(p => p.ToString() == action.Port))
                {
                    var ev = new SignalReceivedEvent(action.Port, Comp<KiasGridComponent>(grid).Core);
                    RaiseLocalEvent(target, ref ev);
                }
                break;
        }
    }

    public bool Mayday(EntityUid grid, string reason)
    {
        if (!_kias.HasRole(grid, KiasDeviceRole.Navigation) || !TryProtocol(grid, out var protocols)
            || protocols.MaydayAfter > _timing.CurTime)
            return false;
        var runtime = Comp<KiasGridComponent>(grid);
        if (runtime.Testing)
            return false;
        var source = runtime.Online.FirstOrDefault(uid => Comp<KiasDeviceComponent>(uid).Role == KiasDeviceRole.Navigation && _kias.IsOnline(uid));
        if (!source.Valid)
            return false;
        protocols.MaydayAfter = _timing.CurTime + TimeSpan.FromSeconds(60);
        var message = Loc.GetString("kias-mayday", ("ship", Name(grid)), ("reason", reason[..Math.Min(reason.Length, 256)]));
        _radio.SendRadioMessage(source, message, "Common", source);
        _shuttles.SetKiasMayday(grid, true);
        _safety.Publish(grid, message, true);
        return true;
    }

    private void OnAvailability(ref KiasAvailabilityChangedEvent args)
    {
        if (!args.Active)
        {
            _shuttles.SetKiasMayday(args.Grid, false);
            if (!TryComp<KiasGridComponent>(args.Grid, out var runtime))
                return;
            foreach (var uid in runtime.Devices)
            {
                if (!TryComp<KiasProtocolComponent>(uid, out var protocols))
                    continue;
                protocols.Cooldowns.Clear();
                protocols.RecentDamage = 0;
                protocols.DamageWindow = TimeSpan.Zero;
                protocols.Alert = KiasAlert.Normal;
            }
        }
    }

    private void OnControl(Entity<KiasDisplayComponent> ent, ref KiasControlMessage args)
    {
        if (Transform(ent).GridUid is not { } grid || !_kias.IsOnline(ent) || !_kias.IsOwner(grid, args.Actor)
            || !TryProtocol(grid, out var protocols))
            return;
        if (args.Reset)
        {
            protocols.Alert = KiasAlert.Normal;
            _shuttles.SetKiasMayday(grid, false);
            _display.RefreshOpen(grid);
        }
        else
            Trigger(grid, KiasTrigger.Manual);
    }

    private void OnConfigure(Entity<KiasDisplayComponent> ent, ref KiasProtocolMessage args)
    {
        if (Transform(ent).GridUid is not { } grid || !_kias.IsOnline(ent) || !_kias.IsOwner(grid, args.Actor)
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
            protocols.Revision++;
            _display.Refresh(ent);
            return;
        }
        if (!Enum.IsDefined(args.Trigger) || !Enum.IsDefined(args.Action) || !float.IsFinite(args.Cooldown)
            || !float.IsFinite(args.MinimumValue) || args.Disposition is { } disposition && !Enum.IsDefined(disposition))
            return;
        var target = args.Target is { } net ? GetEntity(net) : (EntityUid?) null;
        if (target is { } uid && (TerminatingOrDeleted(uid) || Transform(uid).GridUid != grid))
            return;
        var record = new KiasProtocolRecord { Trigger = args.Trigger, Enabled = args.Enabled, Cooldown = Math.Clamp(args.Cooldown, 1, 600),
            Disposition = args.Disposition, MinimumValue = Math.Clamp(args.MinimumValue, 0, 1000000), RequireCrewUnavailable = args.RequireCrewUnavailable };
        record.Actions.Add(new KiasProtocolAction { Kind = args.Action, Target = target, Value = args.Value,
            Group = args.Group.Trim()[..Math.Min(args.Group.Trim().Length, 32)], Port = args.Port.Trim()[..Math.Min(args.Port.Trim().Length, 64)],
            Message = args.Message.Trim()[..Math.Min(args.Message.Trim().Length, 256)] });
        if (args.Index == protocols.Protocols.Count)
            protocols.Protocols.Add(record);
        else
        {
            record.Actions.AddRange(protocols.Protocols[args.Index].Actions.Skip(1).Take(7));
            protocols.Protocols[args.Index] = record;
        }
        protocols.Cooldowns.Remove(args.Index);
        protocols.Revision++;
        _display.Refresh(ent);
    }
}
