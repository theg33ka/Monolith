using System.Linq;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Server._Forge.KIAS;

public sealed class KiasIntegrationSystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DeviceLinkSystem _links = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<EntityUid> _pendingRooms = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<KiasIntegrationKitComponent, AfterInteractEvent>(OnInstall);
        SubscribeLocalEvent<KiasIntegratedComponent, SignalReceivedEvent>(OnSignal);
        EntityManager.EntityInitialized += OnNativeInitialized;
        SubscribeLocalEvent<ApcPowerReceiverComponent, AnchorStateChangedEvent>(OnNativeAnchor);
    }

    public override void Shutdown()
    {
        EntityManager.EntityInitialized -= OnNativeInitialized;
        _pendingRooms.Clear();
    }

    private void OnNativeInitialized(Entity<MetaDataComponent> ent)
    {
        if (Transform(ent).GridUid is { } grid && (HasComp<DoorComponent>(ent) || HasComp<AtmosMonitorComponent>(ent)
            || HasComp<AirAlarmComponent>(ent) || HasComp<FireAlarmComponent>(ent)
            || HasComp<GasVentPumpComponent>(ent) || HasComp<GasVentScrubberComponent>(ent))) _pendingRooms.Add(grid);
    }

    private void OnNativeAnchor(Entity<ApcPowerReceiverComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (Transform(ent).GridUid is { } grid && (HasComp<DoorComponent>(ent) || HasComp<AtmosMonitorComponent>(ent)
            || HasComp<AirAlarmComponent>(ent) || HasComp<FireAlarmComponent>(ent)
            || HasComp<GasVentPumpComponent>(ent) || HasComp<GasVentScrubberComponent>(ent))) _pendingRooms.Add(grid);
    }

    public override void Update(float frameTime)
    {
        using var measurement = new KiasUpdateMeasurement(_kias);
        if (_pendingRooms.Count == 0) return;
        foreach (var grid in _pendingRooms.Take(4).ToArray())
        {
            _pendingRooms.Remove(grid);
            if (!TerminatingOrDeleted(grid)) EntityManager.System<KiasCrewSystem>().RebuildCoverage(grid);
        }
    }

    public bool CanControl(EntityUid target)
    {
        if (TerminatingOrDeleted(target) || !Transform(target).Anchored || Transform(target).GridUid is not { } grid
            || !TryComp<KiasGridComponent>(grid, out var runtime) || !runtime.Active || runtime.Core is not { } core
            || !TryComp<MapGridComponent>(grid, out var map)) return false;
        var connection = target;
        if (TryComp<KiasIntegratedComponent>(target, out var integrated) && !integrated.Direct)
        {
            if (integrated.Scanner is not { } scanner || TerminatingOrDeleted(scanner) || Transform(scanner).GridUid != grid
                || !TryComp<KiasRoomScannerComponent>(scanner, out var module) || (module.Modules & KiasScannerModules.Connector) == 0
                || !_power.IsPowered(scanner) || !Transform(scanner).Anchored) return false;
            var a = _maps.TileIndicesFor(grid, map, Transform(scanner).Coordinates);
            var b = _maps.TileIndicesFor(grid, map, Transform(target).Coordinates);
            if ((a - b).LengthSquared > module.Range * module.Range) return false;
            connection = scanner;
        }
        return runtime.Topology.Connected(_maps.TileIndicesFor(grid, map, Transform(core).Coordinates),
            _maps.TileIndicesFor(grid, map, Transform(connection).Coordinates));
    }

    private void OnInstall(Entity<KiasIntegrationKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || Transform(target).GridUid is not { } grid
            || !_kias.CanConfigure(grid, args.User) || TryComp<KiasIntegratedComponent>(target, out var existing) && existing.Direct
            || !(HasComp<ApcPowerReceiverComponent>(target) || HasComp<Content.Shared.Radio.Components.RadioJammerComponent>(target))
            || HasComp<KiasCoreComponent>(target) || !Transform(target).Anchored) return;
        Integrate(target, null);
        args.Handled = true;
        QueueDel(ent);
    }

    private void Integrate(EntityUid target, EntityUid? scanner)
    {
        var integrated = EnsureComp<KiasIntegratedComponent>(target);
        integrated.Scanner = scanner;
        integrated.Direct = scanner == null;
        EnsureComp<KiasDeviceComponent>(target).Role = KiasDeviceRole.Adapter;
        _links.EnsureSinkPorts(target, "On", "Off");
        _links.EnsureSourcePorts(target, "Status");
        if (Transform(target).GridUid is { } grid) _kias.Invalidate(grid);
    }

    public void ConnectRoom(EntityUid scanner, int range)
    {
        if (!_kias.IsOnline(scanner) || Transform(scanner).GridUid is not { } grid) return;
        _nearby.Clear();
        _lookup.GetEntitiesInRange(scanner, Math.Clamp(range, 0, 10), _nearby, LookupFlags.All);
        foreach (var target in _nearby)
        {
            if (Transform(target).GridUid != grid || HasComp<KiasDeviceComponent>(target)
                && (!TryComp<KiasIntegratedComponent>(target, out var integrated) || integrated.Direct || CanControl(target))) continue;
            if (HasComp<AirAlarmComponent>(target) || HasComp<FireAlarmComponent>(target) || HasComp<AtmosMonitorComponent>(target)
                || HasComp<GasVentPumpComponent>(target) || HasComp<GasVentScrubberComponent>(target) || HasComp<DoorComponent>(target))
                Integrate(target, scanner);
        }
    }

    private void OnSignal(Entity<KiasIntegratedComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port is not ("On" or "Off") || !CanControl(ent)
            || Comp<KiasGridComponent>(Transform(ent).GridUid!.Value).Testing) return;
        if (args.Trigger is { } source && (Transform(source).GridUid != Transform(ent).GridUid || !_kias.IsOnline(source))) return;
        var enabled = args.Port == "On";
        if (HasComp<Content.Shared.Radio.Components.RadioJammerComponent>(ent))
        {
            if (!_kias.HasRole(Transform(ent).GridUid!.Value, KiasDeviceRole.Defence)) return;
            enabled = EntityManager.System<Content.Server.Radio.EntitySystems.JammerSystem>().SetEnabled(ent, enabled);
        }
        _power.SetPowerDisabled(ent, !enabled);
        _links.SendSignal(ent, "Status", enabled);
    }
}
