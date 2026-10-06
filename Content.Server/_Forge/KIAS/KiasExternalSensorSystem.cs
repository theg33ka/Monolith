using System.Linq;
using System.Numerics;
using Content.Shared._Forge.KIAS;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Forge.KIAS;

public sealed class KiasExternalSensorSystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private KiasNavigationSystem _navigation = default!;
    [Dependency] private KiasSafetySystem _safety = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _proximity = new();
    private readonly Dictionary<EntityUid, TimeSpan> _flashAfter = new();
    private TimeSpan _nextScan;

    public override void Initialize()
    {
        SubscribeLocalEvent<KiasWeaponFiredEvent>(OnFired);
        SubscribeLocalEvent<KiasTopologyChangedEvent>(OnTopology);
        SubscribeLocalEvent<KiasAvailabilityChangedEvent>(OnAvailability);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
        SubscribeLocalEvent<KiasWeaponFlashEvent>(OnFlash);
        SubscribeLocalEvent<KiasProximityEvent>(OnProximity);
    }

    private void OnGridRemoved(GridRemovalEvent args) => Clear(args.EntityUid);
    private void OnAvailability(ref KiasAvailabilityChangedEvent args)
    {
        if (!args.Active)
            Clear(args.Grid);
    }

    private void Clear(EntityUid grid)
    {
        _flashAfter.Remove(grid);
        if (!_proximity.Remove(grid, out var sensors))
            return;
        foreach (var uid in sensors)
        {
            if (TryComp<KiasProximityComponent>(uid, out var sensor))
                sensor.Contacts.Clear();
        }
    }

    private void OnTopology(ref KiasTopologyChangedEvent args)
    {
        if (!_kias.HasRole(args.Grid, KiasDeviceRole.Navigation) || !TryComp<KiasGridComponent>(args.Grid, out var runtime))
        {
            Clear(args.Grid);
            return;
        }
        var sensors = runtime.Online.Where(HasComp<KiasProximityComponent>).ToHashSet();
        if (_proximity.TryGetValue(args.Grid, out var previous))
        {
            foreach (var removed in previous.Except(sensors))
            {
                if (TryComp<KiasProximityComponent>(removed, out var sensor))
                    sensor.Contacts.Clear();
            }
        }
        if (sensors.Count > 0)
            _proximity[args.Grid] = sensors;
        else
            _proximity.Remove(args.Grid);
    }

    private void OnFired(ref KiasWeaponFiredEvent args)
    {
        if (_kias.ActiveGrids.Count == 0 || TerminatingOrDeleted(args.Source))
            return;
        var source = _transform.GetMapCoordinates(args.Source);
        var sourceGrid = Transform(args.Source).GridUid;
        var announced = new HashSet<EntityUid>();
        foreach (var detector in _lookup.GetEntitiesInRange<KiasWeaponFlashComponent>(source, 1000))
        {
            if (!_kias.IsOnline(detector) || Transform(detector).GridUid is not { } grid || grid == sourceGrid
                || !_kias.HasRole(grid, KiasDeviceRole.Defence) || announced.Contains(grid)
                || _flashAfter.GetValueOrDefault(grid) > _timing.CurTime)
                continue;
            var position = _transform.GetMapCoordinates(detector);
            var offset = source.Position - position.Position;
            var range = Math.Clamp(detector.Comp.Range, 0, 1000);
            if (source.MapId != position.MapId || offset.LengthSquared() > range * range || offset.LengthSquared() < 0.01f)
                continue;
            var forward = _transform.GetWorldRotation(detector).ToWorldVec();
            if (Vector2.Dot(Vector2.Normalize(offset), forward) < MathF.Cos(MathF.PI / 4))
                continue;
            announced.Add(grid);
            _flashAfter[grid] = _timing.CurTime + TimeSpan.FromSeconds(3);
            var disposition = sourceGrid is { } ship ? _navigation.Classify(grid, ship) : KiasContactDisposition.Unknown;
            var ev = new KiasWeaponFlashEvent(grid, args.Source, disposition);
            RaiseLocalEvent(grid, ref ev, true);
        }
    }

    private void OnFlash(ref KiasWeaponFlashEvent args)
    {
        if (args.Disposition is KiasContactDisposition.Hostile or KiasContactDisposition.Unknown)
            _safety.Publish(args.Grid, Loc.GetString("kias-weapon-flash"), true);
    }

    private void OnProximity(ref KiasProximityEvent args) => _safety.Publish(args.Grid,
        Loc.GetString("kias-proximity-contact", ("range", MathF.Round(args.Distance)),
            ("disposition", Loc.GetString($"kias-contact-{args.Disposition.ToString().ToLowerInvariant()}"))),
        args.Disposition is KiasContactDisposition.Hostile or KiasContactDisposition.Unknown);

    public override void Update(float frameTime)
    {
        if (_proximity.Count == 0 || _timing.CurTime < _nextScan)
            return;
        _nextScan = _timing.CurTime + TimeSpan.FromSeconds(1);
        foreach (var (grid, sensors) in _proximity.ToArray())
        foreach (var uid in sensors)
        {
            if (!_kias.IsOnline(uid) || !TryComp<KiasProximityComponent>(uid, out var sensor))
                continue;
            var position = _transform.GetMapCoordinates(uid);
            var range = Math.Clamp(sensor.Range, 0, 500);
            var nearby = new List<Entity<MapGridComponent>>();
            _map.FindGridsIntersecting(position.MapId, new Box2(position.Position - new Vector2(range), position.Position + new Vector2(range)), ref nearby, includeMap: false);
            var current = new HashSet<EntityUid>();
            foreach (var contact in nearby)
            {
                if (contact.Owner == grid || TerminatingOrDeleted(contact) || HasComp<MapComponent>(contact))
                    continue;
                var target = _transform.ToMapCoordinates(new EntityCoordinates(contact, contact.Comp.LocalAABB.Center));
                var distance = Vector2.Distance(target.Position, position.Position);
                if (distance > range || current.Count >= 16)
                    continue;
                current.Add(contact);
                if (sensor.Contacts.Contains(contact))
                    continue;
                var ev = new KiasProximityEvent(grid, contact, distance, _navigation.Classify(grid, contact));
                RaiseLocalEvent(grid, ref ev, true);
            }
            sensor.Contacts.Clear();
            sensor.Contacts.UnionWith(current);
        }
    }
}
