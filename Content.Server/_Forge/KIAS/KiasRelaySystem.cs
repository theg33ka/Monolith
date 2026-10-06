using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._Forge.KIAS;
using Content.Shared.Interaction;
using Content.Shared.NodeContainer;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;

namespace Content.Server._Forge.KIAS;

public sealed class KiasRelaySystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private NodeGroupSystem _nodes = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedToolSystem _tools = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile, CableType Channel), HashSet<EntityUid>> _open = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<KiasRelayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<KiasRelayComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<KiasRelayComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<KiasRelayComponent, EntParentChangedMessage>(OnParent);
        SubscribeLocalEvent<KiasRelayComponent, GridUidChangedEvent>(OnGrid);
        SubscribeLocalEvent<KiasRelayComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<KiasRelayComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<KiasRelayComponent, InteractUsingEvent>(OnTool);
    }

    private void OnStartup(Entity<KiasRelayComponent> ent, ref ComponentStartup args) => Refresh(ent);
    private void OnShutdown(Entity<KiasRelayComponent> ent, ref ComponentShutdown args) => Unindex(ent);
    private void OnAnchor(Entity<KiasRelayComponent> ent, ref AnchorStateChangedEvent args) => Refresh(ent);
    private void OnParent(Entity<KiasRelayComponent> ent, ref EntParentChangedMessage args) => Refresh(ent);
    private void OnGrid(Entity<KiasRelayComponent> ent, ref GridUidChangedEvent args) => Refresh(ent);
    private void OnMove(Entity<KiasRelayComponent> ent, ref MoveEvent args)
    {
        if (!args.OnlyRotation)
            Refresh(ent);
    }

    public bool IsBlocked(EntityUid grid, Vector2i tile, CableType channel) => _open.ContainsKey((grid, tile, channel));

    public bool IsCableBlocked(EntityUid cable)
    {
        if (_open.Count == 0 || !TryComp<CableComponent>(cable, out var component)
            || Transform(cable).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var map))
            return false;
        return IsBlocked(grid, _map.TileIndicesFor(grid, map, Transform(cable).Coordinates), component.CableType);
    }

    private void Unindex(Entity<KiasRelayComponent> ent)
    {
        if (ent.Comp.Indexed is not { } old)
            return;
        ent.Comp.Indexed = null;
        if (_open.TryGetValue(old, out var relays))
        {
            relays.Remove(ent);
            if (relays.Count == 0)
                _open.Remove(old);
        }
        Invalidate(old.Grid, old.Tile);
    }

    public void Refresh(Entity<KiasRelayComponent> ent)
    {
        Unindex(ent);
        _metadata.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
        var xform = Transform(ent);
        if (TerminatingOrDeleted(ent) || ent.Comp.Closed || !xform.Anchored
            || xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var map))
            return;
        var key = (grid, _map.TileIndicesFor(grid, map, xform.Coordinates), ent.Comp.Channel);
        if (!_open.TryGetValue(key, out var relays))
            _open.Add(key, relays = new());
        relays.Add(ent);
        ent.Comp.Indexed = key;
        Invalidate(grid, key.Item2);
    }

    private void Invalidate(EntityUid grid, Vector2i tile)
    {
        if (!TryComp<MapGridComponent>(grid, out var map) || TerminatingOrDeleted(grid))
            return;
        foreach (var uid in _map.GetAnchoredEntities(grid, map, tile))
        {
            if (!TryComp<NodeContainerComponent>(uid, out var container))
                continue;
            foreach (var node in container.Nodes.Values)
                _nodes.QueueReflood(node);
        }
        _kias.Invalidate(grid);
    }

    private bool CanOperate(EntityUid uid) => !TerminatingOrDeleted(uid) && Transform(uid).Anchored
        && Transform(uid).GridUid is { } grid && _kias.ActiveGrids.Contains(grid) && _power.IsPowered(uid);

    public bool SetClosed(EntityUid uid, bool closed)
    {
        if (!CanOperate(uid) || !TryComp<KiasRelayComponent>(uid, out var relay))
            return false;
        relay.Closed = closed;
        Refresh((uid, relay));
        return true;
    }

    public bool SetChannel(EntityUid uid, CableType channel)
    {
        if (!CanOperate(uid) || !Enum.IsDefined(channel) || !TryComp<KiasRelayComponent>(uid, out var relay))
            return false;
        relay.Channel = channel;
        Refresh((uid, relay));
        return true;
    }

    private void OnVerbs(Entity<KiasRelayComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanOperate(ent))
            return;
        var uid = ent.Owner;
        var closed = !ent.Comp.Closed;
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString(closed ? "kias-relay-close" : "kias-relay-open"), Act = () => SetClosed(uid, closed) });
        foreach (var channel in Enum.GetValues<CableType>())
        {
            var selected = channel;
            args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString($"kias-relay-{channel.ToString().ToLowerInvariant()}"), Act = () => SetChannel(uid, selected) });
        }
    }

    private void OnTool(Entity<KiasRelayComponent> ent, ref InteractUsingEvent args)
    {
        if (!args.Handled && _tools.HasQuality(args.Used, "Screwing") && CanOperate(ent))
        {
            args.Handled = SetChannel(ent, (CableType) (((int) ent.Comp.Channel + 1) % 4));
        }
    }
}
