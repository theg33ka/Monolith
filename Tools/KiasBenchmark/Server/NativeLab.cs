using System.Diagnostics;
using System.Diagnostics.Tracing;
using EventSource = System.Diagnostics.Tracing.EventSource;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Radiation.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Benchmark;

public sealed class NativeLabCommand : IConsoleCommand
{
    public string Command => "native_lab_start";
    public string Description => "Start the isolated native gameplay replay laboratory.";
    public string Help => Command;
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        try { NativeLab.Start(shell); }
        catch (Exception error) { NativeLab.RecordFailure(error); throw; }
    }
}

public static class NativeLab
{
    public static Func<object>? Telemetry;
    public static bool TelemetryRetryRequired;
    private static uint _nextTelemetryCheck;
    private static uint? _unavailableSince;
    private static int _writtenSamples;
    private sealed record Input(string Id, uint Tick, string Ship, string Type, string? Prototype);
    private sealed record Ship(EntityUid Grid, EntityUid Actor, EntityUid PowerDevice, EntityUid Light, Vector2i FireTile);
    private static IEntityManager _em = default!;
    private static IGameTiming _timing = default!;
    private static readonly Dictionary<string, Ship> Ships = new();
    private static Input[] _inputs = Array.Empty<Input>();
    private static StreamWriter? _samples, _events;
    private static uint _start, _warmup, _duration;
    private static int _next;
    private static long _clock, _alloc;
    private static string _output = string.Empty;
    private static readonly List<object> Bindings = new();
    private static IConsoleShell? _shell;
    private static readonly Dictionary<EntityUid, EntityUid> Radiation = new(), Anomalies = new();
    private static readonly Dictionary<EntityUid, EntityUid> Actors = new();
    private static TickSample[] _tickSamples = Array.Empty<TickSample>();
    private static LoopListener? _listener;
    private static readonly Process Process = Process.GetCurrentProcess();
    private static long _workingSet;
    private static double _cpu;
    private struct TickSample
    {
        public uint EngineTick;
        public double WholeMs, ContentMs, Cpu, PauseMs;
        public long WholeBytes, ContentBytes, ThreadBytes, WorkingSet;
        public int Gc0, Gc1, Gc2;
        public bool Captured;
    }
    private sealed class LoopListener : EventListener
    {
        private long _started, _bytes, _threadBytes;
        private TimeSpan _pause;
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Robust.GameLoop") EnableEvents(source, EventLevel.LogAlways);
        }
        protected override void OnEventWritten(EventWrittenEventArgs args)
        {
            if (_samples == null || _timing.CurTick.Value < _start + _warmup) return;
            var relative = _timing.CurTick.Value - _start - _warmup;
            if (relative >= _duration) return;
            if (args.EventId == 4)
            {
                _bytes = GC.GetTotalAllocatedBytes(false);
                _threadBytes = GC.GetAllocatedBytesForCurrentThread();
                _pause = GC.GetTotalPauseDuration();
                _started = Stopwatch.GetTimestamp();
            }
            else if (args.EventId == 5 && _started != 0)
            {
                ref var sample = ref _tickSamples[relative];
                sample.WholeMs = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
                sample.WholeBytes = GC.GetTotalAllocatedBytes(false) - _bytes;
                sample.ThreadBytes = GC.GetAllocatedBytesForCurrentThread() - _threadBytes;
                sample.PauseMs = (GC.GetTotalPauseDuration() - _pause).TotalMilliseconds;
                sample.Gc0 = GC.CollectionCount(0); sample.Gc1 = GC.CollectionCount(1); sample.Gc2 = GC.CollectionCount(2);
                if (relative % 60 == 0)
                {
                    Process.Refresh(); _workingSet = Process.WorkingSet64; _cpu = Process.TotalProcessorTime.TotalSeconds;
                }
                sample.WorkingSet = _workingSet; sample.Cpu = _cpu;
                sample.EngineTick = _timing.CurTick.Value; sample.Captured = true;
                _started = 0;
            }
        }
    }

    public static void Start(IConsoleShell shell)
    {
        if (_samples != null) throw new InvalidOperationException("A laboratory replay is already running.");
        _output = Environment.GetEnvironmentVariable("NATIVE_LAB_OUTPUT") ?? throw new InvalidOperationException("NATIVE_LAB_OUTPUT missing.");
        var scenario = Environment.GetEnvironmentVariable("NATIVE_LAB_SCENARIO") ?? throw new InvalidOperationException("NATIVE_LAB_SCENARIO missing.");
        _warmup = uint.Parse(Environment.GetEnvironmentVariable("NATIVE_LAB_WARMUP") ?? "7200", CultureInfo.InvariantCulture);
        _duration = uint.Parse(Environment.GetEnvironmentVariable("NATIVE_LAB_TICKS") ?? "600", CultureInfo.InvariantCulture);
        var count = int.Parse(Environment.GetEnvironmentVariable("NATIVE_LAB_SHIPS") ?? "40", CultureInfo.InvariantCulture);
        _em = IoCManager.Resolve<IEntityManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _shell = shell;
        if (_timing.TickRate != 60) throw new InvalidOperationException("Native laboratory requires the matched 60 TPS configuration.");
        _inputs = File.ReadLines(scenario).Select(line =>
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            return new Input(root.GetProperty("id").GetString()!, root.GetProperty("tick").GetUInt32(), root.GetProperty("ship").GetString()!, root.GetProperty("type").GetString()!, root.TryGetProperty("prototype", out var prototype) ? prototype.GetString() : null);
        }).OrderBy(input => input.Tick).ThenBy(input => input.Id).ToArray();
        if (_inputs.Any(input => input.Tick >= _duration)) throw new InvalidOperationException("Replay input lies outside the measured interval.");
        Directory.CreateDirectory(_output);
        Ships.Clear();
        Bindings.Clear();
        var maps = _em.System<SharedMapSystem>();
        maps.CreateMap(out var mapId, runMapInit: false);
        for (var i = 1; i <= count; i++)
        {
            if (!_em.System<MapLoaderSystem>().TryLoadGrid(mapId, new ResPath("/Maps/_Forge/Shuttles/Archive/Mercenary/labBriar.yml"), out var loaded)) throw new InvalidOperationException("Briar load failed.");
            var grid = loaded!.Value.Owner;
            _em.System<SharedTransformSystem>().SetWorldPosition(grid, new Vector2(i % 8 * 3000, i / 8 * 3000));
            EntityUid[] Native<T>() where T : Component
            {
                var found = new List<EntityUid>();
                var query = _em.AllEntityQueryEnumerator<T, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out var transform)) if (transform.GridUid == grid) found.Add(uid);
                return found.OrderBy(uid => _em.GetComponent<TransformComponent>(uid).LocalPosition.X).ThenBy(uid => _em.GetComponent<TransformComponent>(uid).LocalPosition.Y).ToArray();
            }
            var actor = _em.SpawnEntity("MobHuman", new EntityCoordinates(grid, new Vector2(-4.5f, .5f)));
            _em.System<SharedMindSystem>().TransferTo(_em.System<SharedMindSystem>().CreateMind(null), actor);
            var power = Native<Content.Server.Atmos.Monitor.Components.AtmosMonitorComponent>().First(uid => _em.HasComponent<Content.Server.Power.Components.ApcPowerReceiverComponent>(uid));
            var light = Native<PoweredLightComponent>().First();
            Ships.Add($"ship-{i:0000}", new Ship(grid, actor, power, light, new Vector2i(-5, 0)));
            Bindings.Add(new { ship = $"ship-{i:0000}", gridPosition = _em.System<SharedTransformSystem>().GetWorldPosition(grid).ToString(),
                powerPrototype = _em.GetComponent<MetaDataComponent>(power).EntityPrototype?.ID,
                powerPosition = _em.GetComponent<TransformComponent>(power).LocalPosition.ToString(),
                lightPrototype = _em.GetComponent<MetaDataComponent>(light).EntityPrototype?.ID,
                lightPosition = _em.GetComponent<TransformComponent>(light).LocalPosition.ToString(), actorPosition = "-4.5,0.5", fireTile = "-5,0" });
        }
        maps.InitializeMap(mapId);
        foreach (var ship in Ships.Values)
        {
            var query = _em.AllEntityQueryEnumerator<BatteryComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var battery, out var transform))
                if (transform.GridUid == ship.Grid) _em.System<BatterySystem>().SetCharge(uid, battery.MaxCharge);
        }
        File.WriteAllText(Path.Combine(_output, "bindings.json"), JsonSerializer.Serialize(Bindings));
        IoCManager.Resolve<IRobustRandom>().SetSeed(20261009);
        _samples = new StreamWriter(Path.Combine(_output, "ticks.csv"));
        _samples.WriteLine("relative_tick,engine_tick,content_engine_ms,allocated_bytes,gc0,gc1,gc2,working_set_bytes,cpu_seconds,whole_tick_ms,whole_allocated_bytes,thread_allocated_bytes,gc_pause_ms");
        _events = new StreamWriter(Path.Combine(_output, "events.jsonl"));
        _start = _timing.CurTick.Value + 1;
        _next = 0; _writtenSamples = 0; _unavailableSince = null; TelemetryRetryRequired = false;
        _tickSamples = new TickSample[_duration];
        _listener = new LoopListener();
        File.WriteAllText(Path.Combine(_output, "started.json"), JsonSerializer.Serialize(new { status = "PARTIAL_NATIVE_DRY_RUN_RUNNING", ships = count, tickRate = _timing.TickRate, measuredTicks = _duration, warmupTicks = _warmup, plannedEvents = _inputs.Length, driverTypes = _inputs.Select(input => input.Type).Distinct().Order().ToArray(), scenarioSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(scenario))).ToLowerInvariant(), metricScope = "Robust.GameLoop TickStart to TickStop includes all tick handlers, replay dispatch and minute checkpoint/telemetry writes; excludes frame input/update outside Tick. Content interval also recorded. CSV checkpointed every minute and on telemetry retries, remainder written after measurement. Working set and CPU sampled every 60 ticks; GC pause is process-wide runtime cumulative delta." }));
        shell.WriteLine($"Native laboratory loaded {count} grids; warmup {_warmup}, measured {_duration} ticks.");
    }

    public static void PreTick()
    {
        try { PreTickCore(); }
        catch (Exception error) { RecordFailure(error); throw; }
    }

    internal static void RecordFailure(Exception error)
    {
        _listener?.Dispose(); _listener = null;
        if (_samples != null)
        {
            var limit = _writtenSamples;
            while (limit < _tickSamples.Length && _tickSamples[limit].Captured) limit++;
            FlushSamples(limit);
        }
        _samples?.Dispose(); _events?.Dispose(); _samples = null; _events = null; _clock = 0;
        if (string.IsNullOrWhiteSpace(_output)) return;
        Directory.CreateDirectory(_output);
        File.WriteAllText(Path.Combine(_output, "failed.json"), JsonSerializer.Serialize(new { status = "FAILED_LOCAL_LAB_EXCEPTION", error = error.Message, deliveredEvents = _next }));
    }

    private static void PreTickCore()
    {
        if (_samples == null || _timing.CurTick.Value < _start) return;
        var elapsed = _timing.CurTick.Value - _start;
        if (elapsed < _warmup) return;
        var tick = elapsed - _warmup;
        if (tick >= _duration) { Finish(); return; }
        if (tick % 3600 == 0 || (TelemetryRetryRequired && tick >= _nextTelemetryCheck))
        {
            FlushSamples((int) tick);
            _events!.Flush();
            var progress = JsonSerializer.Serialize(new { relativeTick = tick, measuredTicks = _duration, entityCount = _em.EntityCount, deliveredEvents = _next, telemetry = Telemetry?.Invoke() });
            File.WriteAllText(Path.Combine(_output, "progress.json"), progress);
            File.AppendAllText(Path.Combine(_output, "progress.jsonl"), progress + Environment.NewLine);
            _nextTelemetryCheck = tick + 60;
            if (TelemetryRetryRequired)
            {
                _unavailableSince ??= tick;
                if (tick - _unavailableSince.Value >= 120)
                    throw new InvalidOperationException($"Card availability did not recover within 120 ticks; see progress.json at tick {tick}.");
            }
            else _unavailableSince = null;
        }
        while (_next < _inputs.Length && _inputs[_next].Tick <= tick)
        {
            var input = _inputs[_next++];
            if (input.Tick != tick) throw new InvalidOperationException($"Skipped planned replay tick {input.Tick}, actual {tick}.");
            var ship = Ships[input.Ship];
            var nativeEffect = Apply(ship, input);
            _events!.WriteLine(JsonSerializer.Serialize(new { id = input.Id, ship = input.Ship, type = input.Type, scheduledTick = input.Tick, actualTick = tick, engineTick = _timing.CurTick.Value, nativeEffect }));
        }
        _alloc = GC.GetTotalAllocatedBytes(false);
        _clock = Stopwatch.GetTimestamp();
    }

    private static object Apply(Ship ship, Input input)
    {
        var type = input.Type;
        switch (type)
        {
            case "radiation.start":
                var source = _em.SpawnEntity(null, new EntityCoordinates(ship.Grid, new Vector2(-4.5f, .5f)));
                _em.AddComponent<RadiationSourceComponent>(source).Intensity = 10;
                Radiation.Add(ship.Grid, source);
                return new { enabled = _em.GetComponent<RadiationSourceComponent>(source).Enabled };
            case "radiation.clear":
                _em.DeleteEntity(Radiation[ship.Grid]); Radiation.Remove(ship.Grid);
                return new { enabled = false };
            case "anomaly.start":
                if (input.Prototype != "AnomalyFlesh") throw new InvalidOperationException("Anomaly input must explicitly select AnomalyFlesh.");
                var anomaly = _em.SpawnEntity(input.Prototype, new EntityCoordinates(ship.Grid, new Vector2(-4.5f, .5f)));
                var excluded = new[] { "ElectricityAnomalyComponent", "ElectrifiedComponent", "EmpOnTriggerComponent", "GravityAnomalyComponent", "GravityWellComponent", "RadiationSourceComponent", "RandomWalkComponent" };
                if (_em.GetComponents(anomaly).Any(value => excluded.Contains(value.GetType().Name)))
                    throw new InvalidOperationException("Anomaly fixture has an excluded electronics or physical hazard component.");
                var component = _em.GetComponent<AnomalyComponent>(anomaly);
                _em.System<SharedAnomalySystem>().ChangeAnomalyStability(anomaly, component.GrowthThreshold + .1f - component.Stability);
                Anomalies.Add(ship.Grid, anomaly);
                return new { active = component.Stability > component.GrowthThreshold, prototype = input.Prototype, excludedHazardsAbsent = true };
            case "anomaly.clear":
                _em.DeleteEntity(Anomalies[ship.Grid]); Anomalies.Remove(ship.Grid);
                return new { active = false };
            case "hull.damage":
            case "hull.repair":
                var walls = new List<EntityUid>();
                var wallQuery = _em.AllEntityQueryEnumerator<DamageableComponent, TransformComponent>();
                while (wallQuery.MoveNext(out var wall, out var wallDamage, out var transform))
                    if (transform.GridUid == ship.Grid && wallDamage.Damage.DamageDict.ContainsKey("Structural")) walls.Add(wall);
                var target = walls.OrderBy(uid => _em.GetComponent<TransformComponent>(uid).LocalPosition.X).ThenBy(uid => _em.GetComponent<TransformComponent>(uid).LocalPosition.Y).First();
                var structural = new DamageSpecifier(); structural.DamageDict.Add("Structural", type == "hull.damage" ? 50 : -_em.GetComponent<DamageableComponent>(target).Damage.DamageDict["Structural"]);
                _em.System<DamageableSystem>().TryChangeDamage(target, structural);
                return new { damaged = _em.GetComponent<DamageableComponent>(target).Damage.DamageDict["Structural"] > 0 };
            case "fire.start":
            case "fire.clear":
                var atmos = _em.System<AtmosphereSystem>();
                var gas = atmos.GetTileMixture((ship.Grid, null, null), null, ship.FireTile, true) ?? throw new InvalidOperationException("Fire fixture lacks real air.");
                gas.Clear(); gas.SetMoles(Gas.Oxygen, 21); gas.SetMoles(Gas.Nitrogen, 79);
                gas.Temperature = type == "fire.start" ? 1000 : 293.15f;
                if (type == "fire.start") { gas.SetMoles(Gas.Plasma, 5); atmos.HotspotExpose((ship.Grid, null), ship.FireTile, 1000, 100); }
                else atmos.HotspotExtinguish(ship.Grid, ship.FireTile);
                return new { burning = atmos.IsHotspotActive(ship.Grid, ship.FireTile), gas.Temperature };
            case "power.loss":
            case "power.restore":
                _em.System<SharedPowerReceiverSystem>().SetPowerDisabled(ship.PowerDevice, type == "power.loss");
                return new { disabled = _em.GetComponent<Content.Server.Power.Components.ApcPowerReceiverComponent>(ship.PowerDevice).PowerDisabled };
            case "crew.critical":
            case "crew.recover":
                var actor = Actors.GetValueOrDefault(ship.Grid, ship.Actor);
                if (type == "crew.critical")
                {
                    _em.DeleteEntity(actor);
                    actor = _em.SpawnEntity("MobHuman", new EntityCoordinates(ship.Grid, new Vector2(-4.5f, .5f)));
                    _em.System<SharedMindSystem>().TransferTo(_em.System<SharedMindSystem>().CreateMind(null), actor);
                    Actors[ship.Grid] = actor;
                }
                var damage = new DamageSpecifier();
                foreach (var (kind, value) in _em.GetComponent<DamageableComponent>(actor).Damage.DamageDict)
                    damage.DamageDict.Add(kind, type == "crew.critical" ? (kind == "Bloodloss" ? 150 : 0) : -value);
                _em.System<DamageableSystem>().TryChangeDamage(actor, damage);
                var mobState = _em.GetComponent<MobStateComponent>(actor).CurrentState;
                return new { damage = _em.GetComponent<DamageableComponent>(actor).TotalDamage.ToString(), mobState = mobState.ToString(), replacementActor = true };
            case "light.power_off":
            case "light.power_on":
                _em.System<SharedPoweredLightSystem>().SetState(ship.Light, type == "light.power_on");
                return new { on = _em.GetComponent<PoweredLightComponent>(ship.Light).On };
            default: throw new InvalidOperationException($"Unimplemented native replay driver: {type}");
        }
    }

    public static void PostTick()
    {
        if (_samples == null || _clock == 0) return;
        var milliseconds = Stopwatch.GetElapsedTime(_clock).TotalMilliseconds;
        var bytes = GC.GetTotalAllocatedBytes(false) - _alloc;
        var tick = _timing.CurTick.Value - _start - _warmup;
        _tickSamples[tick].ContentMs = milliseconds;
        _tickSamples[tick].ContentBytes = bytes;
        _clock = 0;
    }

    private static void Finish()
    {
        _listener?.Dispose(); _listener = null;
        FlushSamples(_tickSamples.Length);
        _samples!.Dispose(); _events!.Dispose(); _samples = null; _events = null;
        if (_next != _inputs.Length) throw new InvalidOperationException("Not all scheduled inputs were delivered.");
        File.WriteAllText(Path.Combine(_output, "completed.json"), JsonSerializer.Serialize(new { status = "PARTIAL_NATIVE_DRY_RUN_COMPLETE", measuredTicks = _duration, deliveredEvents = _next, ships = Ships.Count }));
        _shell!.WriteLine("Native laboratory completed. This is a partial driver dry run, not the full benchmark.");
    }

    private static void FlushSamples(int limit)
    {
        for (; _writtenSamples < limit; _writtenSamples++)
        {
            var tick = _writtenSamples;
            var sample = _tickSamples[tick];
            if (!sample.Captured) throw new InvalidOperationException($"Whole-tick sample missing at {tick}.");
            _samples!.WriteLine(FormattableString.Invariant($"{tick},{sample.EngineTick},{sample.ContentMs:R},{sample.ContentBytes},{sample.Gc0},{sample.Gc1},{sample.Gc2},{sample.WorkingSet},{sample.Cpu:R},{sample.WholeMs:R},{sample.WholeBytes},{sample.ThreadBytes},{sample.PauseMs:R}"));
        }
        _samples!.Flush();
    }
}
