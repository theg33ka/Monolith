using System.Linq;
using Content.Shared._Forge.KIAS;
using Content.Shared._Forge.KIAS.Controllers;
using Content.Shared.Power;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Forge.KIAS.Controllers;

public sealed partial class KiasControllerRuntimeSystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private KiasControllerPhysicalSystem _physical = default!;
    [Dependency] private KiasControllerIoSystem _io = default!;
    [Dependency] private IGameTiming _timing = default!;
    private sealed class Runtime
    {
        public EntityUid Card, Rack, Grid;
        public uint Revision;
        public long Epoch;
        public KiasGraphMachine? Machine;
        public string Status = "INVALID";
        public string Fault = string.Empty;
        public readonly Dictionary<int, List<EntityUid>> Matches = new();
        public readonly Dictionary<int, Timer> Timers = new();
        public readonly List<(EntityUid Device, string Profile, string Port)> Specific = new();
        public readonly List<(EntityUid Grid, string Profile, string Port)> Selectors = new();
    }
    private sealed class Cause
    {
        public readonly Dictionary<(EntityUid Card, int Node, EntityUid Device, string Port), int> Commands = new();
    }
    private Cause? _cause;
    private sealed record Timer(Runtime Runtime, int Node, uint Token, double Due, long Sequence);
    private sealed record Actuation(Runtime Runtime, KiasControllerNode Node, EntityUid? Target, string Port,
        KiasGraphValue Value, Dictionary<string, KiasGraphValue> Inputs, Cause Cause);
    private readonly SortedSet<Timer> _timers = new(Comparer<Timer>.Create((a, b) =>
        a.Due == b.Due ? a.Sequence.CompareTo(b.Sequence) : a.Due.CompareTo(b.Due)));
    private readonly Dictionary<EntityUid, Runtime> _cards = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _racks = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _grids = new();
    private readonly Dictionary<(EntityUid Device, string Profile, string Port), List<(Runtime Runtime, int Node)>> _specific = new();
    private readonly Dictionary<(EntityUid Grid, string Profile, string Port), List<(Runtime Runtime, int Node)>> _selectors = new();
    private readonly Queue<(EntityUid Rack, EntityUid Card)> _boots = new();
    private readonly HashSet<EntityUid> _booting = new();
    private readonly Queue<(EntityUid Device, string Profile, string Port, KiasGraphValue Value, Cause Cause, long Epoch)> _events = new();
    private readonly Queue<(Runtime Runtime, int Node, EntityUid Device, string Port, KiasGraphValue Value, Cause Cause)> _work = new();
    private readonly Queue<Actuation> _commands = new();
    private EntityUid? _closingGrid;
    private readonly Queue<(EntityUid Device, string Profile, string Port, KiasGraphValue Value, Cause Cause, long Epoch)> _finalEvents = new();
    private readonly Queue<Actuation> _finalCommands = new();
    private long _sequence, _eventEpoch;
    public int RunningCount(EntityUid rack) => _racks.TryGetValue(rack, out var cards)
        ? cards.Count(uid => _cards.TryGetValue(uid, out var runtime) && Valid(runtime)) : 0;
    public string Status(EntityUid card) => _cards.TryGetValue(card, out var runtime)
        ? runtime.Machine?.Fault.Length > 0 ? "FAULT" : runtime.Status : "OFFLINE";
    public string Fault(EntityUid card) => _cards.TryGetValue(card, out var runtime)
        ? runtime.Machine?.Fault.Length > 0 ? runtime.Machine.Fault : runtime.Fault : string.Empty;
    public bool Running(EntityUid card) => _cards.TryGetValue(card, out var runtime) && Valid(runtime);
    public KiasGraphValue LastValue(EntityUid card, int node, string port) =>
        _cards.TryGetValue(card, out var runtime) && runtime.Machine != null ? runtime.Machine.Value(node, port) : default;

    public override void Initialize()
    {
        _io.Emitted += OnEmission;
        SubscribeLocalEvent<KiasTopologyChangedEvent>(OnTopology, after: new[] { typeof(KiasControllerIoSystem) });
        SubscribeLocalEvent<KiasAvailabilityChangedEvent>(OnAvailability);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
        SubscribeLocalEvent<KiasControllerRackComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<KiasControllerRackComponent, PowerChangedEvent>(OnPower);
        SubscribeLocalEvent<KiasControllerRackComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<KiasControllerRackComponent, EntParentChangedMessage>(OnParent);
        SubscribeLocalEvent<KiasControllerCardComponent, ComponentShutdown>(OnCardShutdown);
    }
    public override void Shutdown() { _io.Emitted -= OnEmission; base.Shutdown(); }

    private void OnEmission(EntityUid device, string profile, string port, KiasGraphValue value)
    {
        if (!_specific.ContainsKey((device, profile, port))
            && (Transform(device).GridUid is not { } sourceGrid || !_selectors.ContainsKey((sourceGrid, profile, port)))) return;
        var queue = Transform(device).GridUid == _closingGrid && _closingGrid != null ? _finalEvents : _events;
        if (queue.Count < 4096) queue.Enqueue((device, profile, port, value, _cause ?? new(), ++_eventEpoch));
        else
        {
            if (Transform(device).GridUid is { } grid)
                FaultSubscribers(grid, device, profile, port);
        }
    }
    private void FaultSubscribers(EntityUid grid, EntityUid device, string profile, string port)
    {
        if (_specific.TryGetValue((device, profile, port), out var specific))
            foreach (var (runtime, node) in specific.ToArray())
                if (runtime.Matches.TryGetValue(node, out var matches) && matches.Contains(device)) Fail(runtime, "event-queue-limit");
        if (_selectors.TryGetValue((grid, profile, port), out var selectors))
            foreach (var (runtime, node) in selectors.ToArray())
                if (runtime.Matches.TryGetValue(node, out var matches) && matches.Contains(device)) Fail(runtime, "event-queue-limit");
    }

    public override void Update(float frameTime)
    {
        using var measurement = new KiasUpdateMeasurement(_kias);
        var bootStart = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var i = 0; i < 16; i++)
        {
            if (i > 0 && System.Diagnostics.Stopwatch.GetElapsedTime(bootStart).TotalMilliseconds >= 4
                || !_boots.TryDequeue(out var boot)) break;
            _booting.Remove(boot.Card);
            if (!_cards.ContainsKey(boot.Card) && Available(boot.Rack, boot.Card)) Boot(boot.Rack, boot.Card);
            else if (!TerminatingOrDeleted(boot.Card) && Transform(boot.Card).ParentUid is var parent && HasComp<KiasControllerRackComponent>(parent)) Reconcile(parent);
        }
        var now = _timing.CurTime.TotalSeconds;
        var evaluations = 0;
        for (var i = 0; i < 128 && evaluations < 2048 && _timers.Min is { } timer && timer.Due <= now; i++)
        {
            _timers.Remove(timer);
            timer.Runtime.Timers.Remove(timer.Node);
            if (Valid(timer.Runtime))
            {
                timer.Runtime.Machine!.TimerElapsed(timer.Node, timer.Token);
                evaluations += timer.Runtime.Machine.LastEvaluations;
                if (!timer.Runtime.Machine.Active) Fail(timer.Runtime, timer.Runtime.Machine.Fault);
            }
            else Stop(timer.Runtime.Card);
        }
        for (var i = 0; i < 64 && evaluations < 2048 && _events.TryDequeue(out var ev); i++)
        {
            if (!_kias.IsOnline(ev.Device) || Transform(ev.Device).GridUid is not { } grid) continue;
            if (_specific.TryGetValue((ev.Device, ev.Profile, ev.Port), out var specific)) QueueWork(specific, ev.Device, ev.Port, ev.Value, ev.Cause, ev.Epoch);
            if (_selectors.TryGetValue((grid, ev.Profile, ev.Port), out var selectors)) QueueWork(selectors, ev.Device, ev.Port, ev.Value, ev.Cause, ev.Epoch);
        }
        for (var i = 0; i < 256 && evaluations < 2048 && _work.TryDequeue(out var work); i++)
        {
            var runtime = work.Runtime;
            if (!Valid(runtime) || !_kias.IsOnline(work.Device)
                || !runtime.Matches.TryGetValue(work.Node, out var matches) || !matches.Contains(work.Device)) continue;
            _cause = work.Cause;
            try { runtime.Machine!.EmitExternal(work.Node, work.Port, work.Value, work.Device); }
            finally { _cause = null; }
            evaluations += runtime.Machine.LastEvaluations;
            if (!runtime.Machine.Active) Fail(runtime, runtime.Machine.Fault);
        }
        for (var i = 0; i < 128 && _commands.TryDequeue(out var command); i++)
        {
            Execute(command);
        }
    }

    private void Execute(Actuation command)
    {
        if (!Valid(command.Runtime)) return;
        var target = command.Target ?? command.Runtime.Matches.GetValueOrDefault(command.Node.Id)?
            .FirstOrDefault(uid => _kias.IsOnline(uid) && Transform(uid).GridUid == command.Runtime.Grid);
        if (target is not { } device || device == default
            || !command.Runtime.Matches.TryGetValue(command.Node.Id, out var current) || !current.Contains(device)) return;
        var key = (command.Runtime.Card, command.Node.Id, device, command.Port);
        var count = command.Cause.Commands.GetValueOrDefault(key);
        if (count >= 32 || count == 0 && command.Cause.Commands.Count >= 4096)
        { Fail(command.Runtime, "external-feedback-budget"); return; }
        command.Cause.Commands[key] = count + 1;
        _cause = command.Cause;
        try
        {
            _io.Command(command.Runtime.Grid, command.Runtime.Card, device, command.Node, command.Port, command.Value,
                input => command.Inputs.GetValueOrDefault(input));
        }
        catch (Exception exception)
        {
            Fail(command.Runtime, "actuator-exception");
            Log.Error($"KIAS controller {command.Runtime.Card}, node {command.Node.Id}: {exception}");
        }
        finally { _cause = null; }
    }

    public void FinishBeforeShutdown(EntityUid grid, Action publish)
    {
        if (_closingGrid != null) return;
        _closingGrid = grid;
        var evaluations = 0;
        var commands = 0;
        try
        {
            publish();
            while ((_finalEvents.Count > 0 || _finalCommands.Count > 0) && evaluations < 4096 && commands < 1024)
            {
                for (var i = 0; i < 64 && evaluations < 4096 && _finalEvents.TryDequeue(out var ev); i++)
                {
                    void Deliver(List<(Runtime Runtime, int Node)> endpoints)
                    {
                        foreach (var (runtime, node) in endpoints.ToArray())
                        {
                            if (!Valid(runtime) || runtime.Epoch >= ev.Epoch || !_kias.IsOnline(ev.Device)
                                || !runtime.Matches.TryGetValue(node, out var matches) || !matches.Contains(ev.Device)) continue;
                            _cause = ev.Cause;
                            try { runtime.Machine!.EmitExternal(node, ev.Port, ev.Value, ev.Device); }
                            finally { _cause = null; }
                            evaluations += runtime.Machine.LastEvaluations;
                            if (!runtime.Machine.Active) Fail(runtime, runtime.Machine.Fault);
                            if (evaluations >= 4096) break;
                        }
                    }
                    if (_specific.TryGetValue((ev.Device, ev.Profile, ev.Port), out var specific)) Deliver(specific);
                    if (_selectors.TryGetValue((grid, ev.Profile, ev.Port), out var selectors)) Deliver(selectors);
                }
                for (var i = 0; i < 128 && commands < 1024 && _finalCommands.TryDequeue(out var command); i++)
                { Execute(command); commands++; }
            }
        }
        finally { _closingGrid = null; _cause = null; _finalEvents.Clear(); _finalCommands.Clear(); }
    }

    private void QueueWork(List<(Runtime Runtime, int Node)> endpoints, EntityUid device, string port, KiasGraphValue value, Cause cause, long epoch)
    {
        foreach (var (runtime, node) in endpoints.ToArray())
        {
            if (!Valid(runtime) || runtime.Epoch >= epoch || !runtime.Matches.TryGetValue(node, out var matches) || !matches.Contains(device)) continue;
            if (_work.Count < 4096) _work.Enqueue((runtime, node, device, port, value, cause));
            else Fail(runtime, "work-queue-limit");
        }
    }

    private bool Available(EntityUid rack, EntityUid card) => _kias.IsOnline(rack)
        && !TerminatingOrDeleted(card) && TryComp<KiasControllerCardComponent>(card, out var component) && component.Enabled
        && Transform(card).ParentUid == rack && Enumerable.Range(0, 8).Any(index => _physical.Card(rack, KiasControllerRackComponent.SlotId(index)) == card);
    private bool Valid(Runtime runtime) => runtime.Machine is { Active: true } && Available(runtime.Rack, runtime.Card)
        && Transform(runtime.Rack).GridUid == runtime.Grid && Comp<KiasControllerCardComponent>(runtime.Card).Revision == runtime.Revision;

    public void ContainerChanged(EntityUid rack, EntityUid? removed = null)
    {
        if (removed is { } card) Stop(card);
        Reconcile(rack);
    }
    private void OnShutdown(Entity<KiasControllerRackComponent> ent, ref ComponentShutdown args) => StopRack(ent);
    private void OnCardShutdown(Entity<KiasControllerCardComponent> ent, ref ComponentShutdown args) => Stop(ent);
    private void OnPower(Entity<KiasControllerRackComponent> ent, ref PowerChangedEvent args) { if (!args.Powered) StopRack(ent); else Reconcile(ent); }
    private void OnAnchor(Entity<KiasControllerRackComponent> ent, ref AnchorStateChangedEvent args) { if (!args.Anchored) StopRack(ent); }
    private void OnParent(Entity<KiasControllerRackComponent> ent, ref EntParentChangedMessage args) => StopRack(ent);
    private void OnAvailability(ref KiasAvailabilityChangedEvent args) { if (!args.Active) StopGrid(args.Grid); }
    private void OnGridRemoved(GridRemovalEvent args) => StopGrid(args.EntityUid);
    private void OnTopology(ref KiasTopologyChangedEvent args)
    {
        if (!TryComp<KiasGridComponent>(args.Grid, out var grid)) { StopGrid(args.Grid); return; }
        if (!_grids.TryGetValue(args.Grid, out var previous)) _grids[args.Grid] = previous = new();
        foreach (var rack in previous.ToArray())
            if (TerminatingOrDeleted(rack) || Transform(rack).GridUid != args.Grid || !grid.Devices.Contains(rack)) { StopRack(rack); previous.Remove(rack); }
        foreach (var rack in grid.Devices)
            if (HasComp<KiasControllerRackComponent>(rack)) { previous.Add(rack); Reconcile(rack); NotifyRack(rack); }
    }

    public void Reconcile(EntityUid rack)
    {
        if (!_kias.IsOnline(rack)) { StopRack(rack); return; }
        if (!_racks.TryGetValue(rack, out var known)) _racks[rack] = known = new();
        var inserted = new HashSet<EntityUid>();
        for (var i = 0; i < 8; i++)
        {
            if (_physical.Card(rack, KiasControllerRackComponent.SlotId(i)) is not { } card || !Available(rack, card)) continue;
            inserted.Add(card);
            if (_cards.TryGetValue(card, out var runtime))
            {
                if (runtime.Rack != rack || runtime.Grid != Transform(rack).GridUid || runtime.Revision != Comp<KiasControllerCardComponent>(card).Revision) Stop(card);
                else { if (runtime.Machine is { Active: true }) Index(runtime); continue; }
            }
            if (_booting.Add(card)) _boots.Enqueue((rack, card));
        }
        foreach (var removed in known.Except(inserted).ToArray()) Stop(removed);
        _racks[rack] = inserted;
    }

    private void Boot(EntityUid rack, EntityUid card)
    {
        var component = Comp<KiasControllerCardComponent>(card);
        var runtime = new Runtime { Rack = rack, Card = card, Grid = Transform(rack).GridUid!.Value, Revision = component.Revision, Epoch = _eventEpoch };
        _cards[card] = runtime;
        var compilation = Compile(component.Program);
        if (!compilation.Success) { runtime.Fault = string.Join(", ", compilation.Errors); return; }
        runtime.Machine = new(compilation.Graph!, (node, port, value) => Command(runtime, node, port, value),
            (node, seconds, token) => Schedule(runtime, node, seconds, token), () => _timing.CurTime.TotalSeconds);
        Index(runtime);
        runtime.Status = "RUNNING";
        runtime.Machine.Start(runtime.Matches.ToDictionary(pair => pair.Key, pair => pair.Value.Count));
        if (!runtime.Machine.Active) Fail(runtime, runtime.Machine.Fault);
        NotifyRack(rack);
    }

    private void Schedule(Runtime runtime, int node, double seconds, uint token)
    {
        if (runtime.Timers.Remove(node, out var previous)) _timers.Remove(previous);
        var timer = new Timer(runtime, node, token, _timing.CurTime.TotalSeconds + seconds, ++_sequence);
        runtime.Timers[node] = timer;
        _timers.Add(timer);
    }

    private void Command(Runtime runtime, KiasControllerNode node, string port, KiasGraphValue value)
    {
        if (value.Type is not (KiasPortType.Signal or KiasPortType.Bool)
            || !Available(runtime.Rack, runtime.Card) || !runtime.Matches.TryGetValue(node.Id, out var matches) || matches.Count == 0) return;
        var inputs = runtime.Machine!.Graph.DataInputs.GetValueOrDefault(node.Id, new())
            .ToDictionary(input => input, input => runtime.Machine.InputValue(node.Id, input));
        void Enqueue(EntityUid? target)
        {
            var queue = runtime.Grid == _closingGrid ? _finalCommands : _commands;
            if (queue.Count < 4096) queue.Enqueue(new(runtime, node, target, port, value, inputs, _cause ?? new()));
            else Fail(runtime, "command-queue-limit");
        }
        if (node.Kind == KiasNodeKind.Any) { Enqueue(null); return; }
        foreach (var target in matches.ToArray())
        {
            if (!_kias.IsOnline(target) || Transform(target).GridUid != runtime.Grid) continue;
            Enqueue(target);
            if (!runtime.Machine.Active) break;
        }
    }

    private void Index(Runtime runtime)
    {
        Unindex(runtime);
        if (runtime.Machine == null) return;
        foreach (var node in runtime.Machine.Graph.Nodes.Values)
            if (KiasGraphCatalog.External(node.Kind)) runtime.Matches[node.Id] = _io.Match(runtime.Grid, node);
        foreach (var node in runtime.Machine.Graph.Nodes.Values)
        {
            if (!KiasGraphCatalog.External(node.Kind)) continue;
            foreach (var port in KiasGraphCatalog.DevicePorts(_io.Schema(node.Profile) ?? _io.SnapshotSchema(node) ?? Array.Empty<KiasGraphPort>()))
            {
                if (port.Direction != KiasPortDirection.Output || port.Id.StartsWith('$')) continue;
                if (!runtime.Machine.Graph.Outgoing.ContainsKey(new(node.Id, port.Id))
                    && !(port.Type == KiasPortType.Signal && runtime.Machine.Graph.Outgoing.ContainsKey(new(node.Id, "$Source")))) continue;
                if (node.Kind == KiasNodeKind.Specific && node.Binding is { } device)
                {
                    var key = (device, node.Profile, port.Id);
                    if (!_specific.TryGetValue(key, out var endpoints)) _specific[key] = endpoints = new();
                    endpoints.Add((runtime, node.Id)); runtime.Specific.Add(key);
                }
                else if (node.Kind != KiasNodeKind.Specific)
                {
                    var key = (runtime.Grid, node.Profile, port.Id);
                    if (!_selectors.TryGetValue(key, out var endpoints)) _selectors[key] = endpoints = new();
                    endpoints.Add((runtime, node.Id)); runtime.Selectors.Add(key);
                }
            }
        }
        foreach (var (node, matches) in runtime.Matches) runtime.Machine.MatchCount(node, matches.Count);
    }

    private void Unindex(Runtime runtime)
    {
        foreach (var key in runtime.Specific)
            if (_specific.TryGetValue(key, out var endpoints)) { endpoints.RemoveAll(endpoint => endpoint.Runtime == runtime); if (endpoints.Count == 0) _specific.Remove(key); }
        foreach (var key in runtime.Selectors)
            if (_selectors.TryGetValue(key, out var endpoints)) { endpoints.RemoveAll(endpoint => endpoint.Runtime == runtime); if (endpoints.Count == 0) _selectors.Remove(key); }
        runtime.Specific.Clear(); runtime.Selectors.Clear(); runtime.Matches.Clear();
    }
    private void NotifyRack(EntityUid rack)
    {
        if (!TerminatingOrDeleted(rack) && TryComp<KiasControllerRackComponent>(rack, out var component))
            EntityManager.System<KiasControllerUiSystem>().RefreshRack((rack, component));
        if (!TerminatingOrDeleted(rack) && Transform(rack).GridUid is { } grid)
            EntityManager.System<KiasDisplaySystem>().RefreshOpen(grid);
    }

    private void Fail(Runtime runtime, string reason)
    {
        runtime.Fault = reason; runtime.Status = "FAULT"; runtime.Machine?.Stop(); Unindex(runtime);
        foreach (var timer in runtime.Timers.Values) _timers.Remove(timer);
        runtime.Timers.Clear();
        NotifyRack(runtime.Rack);
    }
    private void Stop(EntityUid card)
    {
        if (!_cards.Remove(card, out var runtime)) return;
        runtime.Machine?.Stop(); Unindex(runtime);
        foreach (var timer in runtime.Timers.Values) _timers.Remove(timer);
        runtime.Timers.Clear();
        NotifyRack(runtime.Rack);
    }
    private void StopRack(EntityUid rack)
    {
        if (!_racks.Remove(rack, out var cards)) return;
        foreach (var card in cards) Stop(card);
    }
    private void StopGrid(EntityUid grid)
    {
        if (!_grids.Remove(grid, out var racks)) return;
        foreach (var rack in racks) StopRack(rack);
    }
}
