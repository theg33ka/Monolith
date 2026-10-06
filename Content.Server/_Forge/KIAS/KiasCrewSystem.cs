using System.Linq;
using System.Text;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Radiation.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.Access.Systems;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Server._Forge.KIAS;

public sealed class KiasCrewSystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedIdCardSystem _ids = default!;
    [Dependency] private DeviceLinkSystem _links = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _people = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _transponders = new();
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, List<EntityUid>>> _coverage = new();
    private TimeSpan _nextScan;

    public override void Initialize()
    {
        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMind);
        SubscribeLocalEvent<MindContainerComponent, PlayerAttachedEvent>(OnPlayer);
        SubscribeLocalEvent<BorgChassisComponent, ComponentInit>(OnBorg);
        SubscribeLocalEvent<KiasTrackedEntityComponent, ComponentStartup>(OnTrackedStartup);
        SubscribeLocalEvent<KiasTrackedEntityComponent, ComponentShutdown>(OnTrackedShutdown);
        SubscribeLocalEvent<KiasTrackedEntityComponent, EntParentChangedMessage>(OnTrackedParent);
        SubscribeLocalEvent<KiasTrackedEntityComponent, GridUidChangedEvent>(OnTrackedGrid);
        SubscribeLocalEvent<KiasTransponderComponent, ComponentStartup>(OnTransponderStartup);
        SubscribeLocalEvent<KiasTransponderComponent, ComponentShutdown>(OnTransponderShutdown);
        SubscribeLocalEvent<KiasTransponderComponent, EntParentChangedMessage>(OnTransponderParent);
        SubscribeLocalEvent<KiasTransponderComponent, GridUidChangedEvent>(OnTransponderGrid);
        SubscribeLocalEvent<KiasTransponderComponent, MapInitEvent>(OnSerial);
        SubscribeLocalEvent<KiasTopologyChangedEvent>(OnTopology);
        SubscribeLocalEvent<KiasAvailabilityChangedEvent>(OnAvailability);
        SubscribeLocalEvent<KiasRoomScannerComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<KiasRoomScannerComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<KiasRoomScannerComponent, ComponentShutdown>(OnScannerShutdown);
        SubscribeLocalEvent<KiasCrewServerComponent, InteractUsingEvent>(OnRegister);
        SubscribeLocalEvent<KiasCrewServerComponent, GetVerbsEvent<AlternativeVerb>>(OnRegistrationVerbs);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoval);
    }

    public bool IsKiasTrackedEntity(EntityUid uid) => !HasComp<GhostComponent>(uid) && HasComp<KiasTrackedEntityComponent>(uid);

    private void OnMind(Entity<MindContainerComponent> ent, ref MindAddedMessage args)
    {
        if (args.Mind.Comp.UserId != null && !HasComp<GhostComponent>(ent))
            EnsureComp<KiasTrackedEntityComponent>(ent);
    }
    private void OnPlayer(Entity<MindContainerComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!HasComp<GhostComponent>(ent))
            EnsureComp<KiasTrackedEntityComponent>(ent);
    }
    private void OnBorg(Entity<BorgChassisComponent> ent, ref ComponentInit args) => EnsureComp<KiasTrackedEntityComponent>(ent);
    private void OnTrackedStartup(Entity<KiasTrackedEntityComponent> ent, ref ComponentStartup args) => Index(ent, ref ent.Comp.IndexedGrid, _people);
    private void OnTrackedParent(Entity<KiasTrackedEntityComponent> ent, ref EntParentChangedMessage args) => Index(ent, ref ent.Comp.IndexedGrid, _people);
    private void OnTrackedGrid(Entity<KiasTrackedEntityComponent> ent, ref GridUidChangedEvent args) => Index(ent, ref ent.Comp.IndexedGrid, _people);
    private void OnTrackedShutdown(Entity<KiasTrackedEntityComponent> ent, ref ComponentShutdown args) => RemoveIndex(ent, ref ent.Comp.IndexedGrid, _people);
    private void OnTransponderStartup(Entity<KiasTransponderComponent> ent, ref ComponentStartup args) => Index(ent, ref ent.Comp.IndexedGrid, _transponders);
    private void OnTransponderParent(Entity<KiasTransponderComponent> ent, ref EntParentChangedMessage args) => Index(ent, ref ent.Comp.IndexedGrid, _transponders);
    private void OnTransponderGrid(Entity<KiasTransponderComponent> ent, ref GridUidChangedEvent args) => Index(ent, ref ent.Comp.IndexedGrid, _transponders);
    private void OnTransponderShutdown(Entity<KiasTransponderComponent> ent, ref ComponentShutdown args) => RemoveIndex(ent, ref ent.Comp.IndexedGrid, _transponders);
    private void OnSerial(Entity<KiasTransponderComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.Serial))
            ent.Comp.Serial = Guid.NewGuid().ToString("N");
    }

    private void RemoveIndex(EntityUid uid, ref EntityUid? oldGrid, Dictionary<EntityUid, HashSet<EntityUid>> index)
    {
        if (oldGrid is { } old && index.TryGetValue(old, out var previous))
        {
            previous.Remove(uid);
            if (previous.Count == 0)
                index.Remove(old);
        }
        oldGrid = null;
    }
    private void Index(EntityUid uid, ref EntityUid? oldGrid, Dictionary<EntityUid, HashSet<EntityUid>> index)
    {
        _metadata.AddFlag(uid, MetaDataFlags.ExtraTransformEvents);
        RemoveIndex(uid, ref oldGrid, index);
        if (TerminatingOrDeleted(uid) || Transform(uid).GridUid is not { } grid)
            return;
        oldGrid = grid;
        if (!index.TryGetValue(grid, out var entities))
            index.Add(grid, entities = new HashSet<EntityUid>());
        entities.Add(uid);
    }

    private void OnModuleInserted(Entity<KiasRoomScannerComponent> ent, ref EntInsertedIntoContainerMessage args) => RebuildCoverage(Transform(ent).GridUid);
    private void OnModuleRemoved(Entity<KiasRoomScannerComponent> ent, ref EntRemovedFromContainerMessage args) => RebuildCoverage(Transform(ent).GridUid);
    private void OnScannerShutdown(Entity<KiasRoomScannerComponent> ent, ref ComponentShutdown args) => RebuildCoverage(Transform(ent).GridUid);
    private void OnTopology(ref KiasTopologyChangedEvent args) => RebuildCoverage(args.Grid);
    private void OnAvailability(ref KiasAvailabilityChangedEvent args)
    {
        if (!args.Active)
            _coverage.Remove(args.Grid);
    }
    private void OnGridRemoval(GridRemovalEvent args)
    {
        _people.Remove(args.EntityUid);
        _transponders.Remove(args.EntityUid);
        _coverage.Remove(args.EntityUid);
    }

    public void RebuildCoverage(EntityUid? grid)
    {
        if (grid is not { } uid || !TryComp<KiasGridComponent>(uid, out var runtime) || !runtime.Active || !TryComp<MapGridComponent>(uid, out var map))
            return;
        var coverage = new Dictionary<Vector2i, List<EntityUid>>();
        foreach (var device in runtime.Online)
        {
            if (!_kias.IsOnline(device) || !TryComp<KiasRoomScannerComponent>(device, out var scanner) || scanner.LifeStage > ComponentLifeStage.Running)
                continue;
            scanner.Modules = KiasScannerModules.None;
            for (var slot = 1; slot <= 6; slot++)
            {
                if (!_containers.TryGetContainer(device, $"kias-module-{slot}", out var container))
                    continue;
                foreach (var item in container.ContainedEntities)
                {
                    if (TryComp<KiasScannerModuleComponent>(item, out var module))
                        scanner.Modules |= module.Module;
                }
            }
            if (scanner.Modules == KiasScannerModules.None)
                continue;
            var tile = _map.TileIndicesFor(uid, map, Transform(device).Coordinates);
            var range = Math.Clamp(scanner.Range, 0, 10);
            for (var x = -range; x <= range; x++)
            for (var y = -range; y <= range; y++)
            {
                if (x * x + y * y > range * range)
                    continue;
                var covered = tile + new Vector2i(x, y);
                if (!coverage.TryGetValue(covered, out var scanners))
                    coverage.Add(covered, scanners = new List<EntityUid>());
                scanners.Add(device);
            }
        }
        if (coverage.Count == 0)
        {
            _coverage.Remove(uid);
            if (runtime.Entities != 0 || runtime.Crew != 0 || runtime.CrewDetails.Length != 0)
            {
                runtime.Entities = 0;
                runtime.Crew = 0;
                runtime.CrewDetails = string.Empty;
                var changed = new KiasCountsChangedEvent(uid, 0, 0);
                RaiseLocalEvent(uid, ref changed, true);
            }
        }
        else
            _coverage[uid] = coverage;
    }

    public bool HasCoverage(EntityUid grid, EntityUid source, KiasScannerModules module)
    {
        if (!_kias.ActiveGrids.Contains(grid) || !_coverage.TryGetValue(grid, out var coverage) || !TryComp<MapGridComponent>(grid, out var map))
            return false;
        var coordinates = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(source));
        return coverage.TryGetValue(_map.TileIndicesFor(grid, map, coordinates), out var scanners)
               && scanners.Any(scanner => (Modules(scanner) & module) != 0);
    }

    private KiasScannerModules Modules(EntityUid scanner) => _kias.IsOnline(scanner) && TryComp<KiasRoomScannerComponent>(scanner, out var component)
        ? component.Modules : KiasScannerModules.None;

    public bool CrewUnavailable(EntityUid grid)
    {
        if (!_kias.HasRole(grid, KiasDeviceRole.Crew) || !TryComp<KiasGridComponent>(grid, out var runtime)
            || !runtime.Online.Any(uid => (Modules(uid) & KiasScannerModules.Biometric) != 0))
            return false;
        var registered = new HashSet<string>();
        foreach (var uid in runtime.Online)
        {
            if (_kias.IsOnline(uid) && TryComp<KiasCrewServerComponent>(uid, out var server))
                registered.UnionWith(server.Registered);
        }
        if (registered.Count == 0)
            return false;
        if (!_transponders.TryGetValue(grid, out var tokens))
            return true;
        foreach (var uid in tokens)
        {
            if (!TryComp<KiasTransponderComponent>(uid, out var token) || token.Core != runtime.Core || !registered.Contains(token.Serial))
                continue;
            var carrier = Transform(uid).ParentUid;
            for (var depth = 0; depth < 16 && carrier.Valid && carrier != grid && !TerminatingOrDeleted(carrier); depth++)
            {
                if (IsKiasTrackedEntity(carrier) && HasCoverage(grid, carrier, KiasScannerModules.Biometric)
                    && (!TryComp<MobStateComponent>(carrier, out var state) || state.CurrentState == MobState.Alive))
                    return false;
                carrier = Transform(carrier).ParentUid;
            }
        }
        return true;
    }

    public string? RoomLabel(EntityUid grid, EntityUid source)
    {
        if (!_coverage.TryGetValue(grid, out var coverage) || !TryComp<MapGridComponent>(grid, out var map))
            return null;
        var coordinates = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(source));
        if (!coverage.TryGetValue(_map.TileIndicesFor(grid, map, coordinates), out var scanners))
            return null;
        foreach (var scanner in scanners.OrderBy(uid => uid.Id))
        {
            if (TryComp<KiasDeviceComponent>(scanner, out var device) && !string.IsNullOrWhiteSpace(device.Room))
                return device.Room;
        }
        return null;
    }

    private void OnRegister(Entity<KiasCrewServerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<KiasTransponderComponent>(args.Used, out var transponder))
            return;
        args.Handled = true;
        if (ent.Comp.RegistrationLocked || !_kias.IsOnline(ent) || Transform(ent).GridUid is not { } grid)
        {
            _popup.PopupEntity(Loc.GetString("kias-registration-locked"), ent, args.User);
            return;
        }
        if (string.IsNullOrEmpty(transponder.Serial))
            transponder.Serial = Guid.NewGuid().ToString("N");
        transponder.Core = Comp<KiasGridComponent>(grid).Core;
        ent.Comp.Registered.Add(transponder.Serial);
        _popup.PopupEntity(Loc.GetString("kias-transponder-registered"), ent, args.User);
    }

    private void OnRegistrationVerbs(Entity<KiasCrewServerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_kias.IsOnline(ent) || Transform(ent).GridUid is not { } grid || !_kias.IsOwner(grid, args.User))
            return;
        var component = ent.Comp;
        var server = ent.Owner;
        var actor = args.User;
        var locked = !component.RegistrationLocked;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(locked ? "kias-lock-registration" : "kias-unlock-registration"),
            Act = () =>
            {
                if (_kias.IsOnline(server) && Transform(server).GridUid == grid && _kias.IsOwner(grid, actor))
                    component.RegistrationLocked = locked;
            },
        });
    }

    public override void Update(float frameTime)
    {
        if (_coverage.Count == 0 || _timing.CurTime < _nextScan)
            return;
        _nextScan = _timing.CurTime + TimeSpan.FromSeconds(1);
        foreach (var (grid, coverage) in _coverage.ToArray())
            Scan(grid, coverage);
    }

    public void RefreshCounts(EntityUid grid)
    {
        if (_coverage.TryGetValue(grid, out var coverage))
            Scan(grid, coverage);
    }

    private void Scan(EntityUid grid, Dictionary<Vector2i, List<EntityUid>> coverage)
    {
        if (!TryComp<KiasGridComponent>(grid, out var runtime) || !runtime.Active || !TryComp<MapGridComponent>(grid, out var map))
            return;
        var counts = new Dictionary<EntityUid, int>();
        var entities = 0;
        var details = new StringBuilder();
        if (_people.TryGetValue(grid, out var people))
        {
            foreach (var person in people)
            {
                if (TerminatingOrDeleted(person) || !IsKiasTrackedEntity(person))
                    continue;
                var position = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(person));
                if (!coverage.TryGetValue(_map.TileIndicesFor(grid, map, position), out var scanners))
                    continue;
                var counted = false;
                foreach (var scanner in scanners)
                {
                    var modules = Modules(scanner);
                    if ((modules & KiasScannerModules.Motion) != 0)
                    {
                        counts[scanner] = counts.GetValueOrDefault(scanner) + 1;
                        counted = true;
                    }
                }
                if (counted)
                    entities++;
                var combined = scanners.Aggregate(KiasScannerModules.None, (flags, scanner) => flags | Modules(scanner));
                if ((combined & KiasScannerModules.Id) != 0 && _ids.TryFindIdCard(person, out var card))
                    details.AppendLine(card.Comp.FullName ?? Name(person));
                if ((combined & KiasScannerModules.Biometric) != 0 && TryComp<MobStateComponent>(person, out var state))
                {
                    var mobState = state.CurrentState;
                    details.AppendLine($"{Name(person)}: {Loc.GetString($"kias-biometric-{mobState.ToString().ToLowerInvariant()}")}");
                }
                if ((combined & KiasScannerModules.Radiation) != 0 && TryComp<RadiationReceiverComponent>(person, out var radiation) && radiation.CurrentRadiation > 0)
                    details.AppendLine(Loc.GetString("kias-radiation", ("name", Name(person)), ("value", radiation.CurrentRadiation)));
            }
        }
        var serials = new HashSet<string>();
        var registered = new HashSet<string>();
        foreach (var device in runtime.Online.ToArray())
        {
            if (TryComp<KiasCrewServerComponent>(device, out var server))
                registered.UnionWith(server.Registered);
            if (TryComp<KiasRoomScannerComponent>(device, out var scanner))
            {
                var count = counts.GetValueOrDefault(device);
                if (scanner.Entities == 0 && count > 0)
                    _links.InvokePort(device, "KiasMotion");
                if (!runtime.Active)
                    return;
                scanner.Entities = count;
            }
        }
        if (_transponders.TryGetValue(grid, out var transponders))
        {
            foreach (var uid in transponders)
            {
                if (TryComp<KiasTransponderComponent>(uid, out var token) && token.Core == runtime.Core && registered.Contains(token.Serial)
                    && HasCoverage(grid, uid, KiasScannerModules.Transponder))
                    serials.Add(token.Serial);
            }
        }
        var text = details.ToString();
        if (runtime.Entities == entities && runtime.Crew == serials.Count && runtime.CrewDetails == text)
            return;
        runtime.Entities = entities;
        runtime.Crew = serials.Count;
        runtime.CrewDetails = text;
        var ev = new KiasCountsChangedEvent(grid, entities, serials.Count);
        RaiseLocalEvent(grid, ref ev, true);
    }
}
