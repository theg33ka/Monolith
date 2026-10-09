using System.Reflection;
using System.Collections;
using System.Text.Json;
using Content.Server._Forge.KIAS;
using Content.Server._Forge.KIAS.Controllers;
using Content.Shared._Forge.KIAS;
using Content.Shared._Forge.KIAS.Controllers;
using Content.Server.Power.Components;
using Content.Shared.Power.Components;
using Content.Server.Benchmark;
using Robust.Shared.GameObjects;

namespace Content.Server.Benchmark;

public sealed class KiasLabTelemetrySystem : EntitySystem
{
    private readonly Dictionary<EntityUid, KiasGraphMachine> _pendingMachines = new();
    public override void Initialize()
    {
        NativeLab.Telemetry = Capture;
    }

    private object Capture()
    {
        var grids = 0; var active = 0; var online = 0; var devices = 0; var running = 0;
        var query = EntityManager.AllEntityQueryEnumerator<KiasGridComponent>();
        while (query.MoveNext(out _, out var grid))
        {
            grids++; if (grid.Active) active++;
            online += grid.Online.Count; devices += grid.Devices.Count;
        }
        var runtime = EntityManager.System<KiasControllerRuntimeSystem>();
        var runtimeCards = (IDictionary) (typeof(KiasControllerRuntimeSystem).GetField("_cards", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime)
            ?? throw new InvalidOperationException("Missing runtime card table."));
        var kias = EntityManager.System<KiasSystem>();
        var dirty = (IReadOnlySet<EntityUid>) (typeof(KiasSystem).GetField("_dirty", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(kias)
            ?? throw new InvalidOperationException("Missing topology dirty set."));
        var alive = 0; var faults = 0; var unavailable = new List<object>(); var dirtyOnly = true;
        var recovered = 0; var sameMachines = 0;
        var cards = EntityManager.AllEntityQueryEnumerator<KiasControllerCardComponent>();
        while (cards.MoveNext(out var uid, out var card))
        {
            var stored = runtimeCards[uid];
            var machine = stored?.GetType().GetField("Machine")?.GetValue(stored) as KiasGraphMachine;
            if (machine?.Active == true) alive++;
            if (runtime.Fault(uid).Length > 0) faults++;
            if (runtime.Running(uid))
            {
                running++;
                if (_pendingMachines.Remove(uid, out var previous)) { recovered++; if (ReferenceEquals(previous, machine)) sameMachines++; }
                continue;
            }
            if (!card.Enabled) continue;
            var gridUid = Transform(uid).GridUid;
            var topologyDirty = gridUid is { } gridId && dirty.Contains(gridId);
            dirtyOnly &= topologyDirty && machine?.Active == true;
            if (machine?.Active == true) _pendingMachines.TryAdd(uid, machine);
            unavailable.Add(new { card = uid.ToString(), program = card.Program.Name, grid = gridUid?.ToString(),
                topologyDirty, machineActive = machine?.Active == true, status = runtime.Status(uid), fault = runtime.Fault(uid) });
        }
        NativeLab.TelemetryRetryRequired = unavailable.Count > 0;
        var availabilityPending = unavailable.Count > 0 && dirtyOnly && alive == grids * 26 && faults == 0;
        var queues = new Dictionary<string, int>();
        foreach (var name in new[] { "_events", "_work", "_commands", "_boots", "_finalEvents", "_finalCommands" })
        {
            var value = typeof(KiasControllerRuntimeSystem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(runtime)
                ?? throw new InvalidOperationException($"Missing laboratory queue metric {name}.");
            queues[name] = (int) (value.GetType().GetProperty("Count")?.GetValue(value)
                ?? throw new InvalidOperationException($"Queue {name} has no Count."));
        }
        var cores = new List<object>();
        var coreQuery = EntityManager.AllEntityQueryEnumerator<KiasCoreComponent, KiasDeviceComponent, TransformComponent>();
        while (coreQuery.MoveNext(out var uid, out var core, out var device, out var transform))
        {
            EntityManager.TryGetComponent<ApcPowerReceiverComponent>(uid, out var power);
            cores.Add(new { core.Enabled, anchored = transform.Anchored, device.Status, powered = power?.Powered, disabled = power?.PowerDisabled,
                received = power?.PowerReceived, load = power?.Load, provider = power?.Provider != null });
        }
        var batteries = EntityManager.AllEntityQueryEnumerator<BatteryComponent>();
        var charge = 0d; var capacity = 0d; var batteryCount = 0;
        while (batteries.MoveNext(out _, out var battery)) { charge += battery.CurrentCharge; capacity += battery.MaxCharge; batteryCount++; }
        return new { grids, activeGrids = active, topologyOnlineDevices = online, devices, runningCards = running,
            activeMachines = alive, faultedCards = faults, unavailableCards = unavailable, topologyDirtyGrids = dirty.Count,
            availabilityPending,
            recoveredCards = recovered, recoveredSameMachines = sameMachines,
            queues, cores, charge, capacity, batteryCount,
            scope = "Minute-boundary samples; topology online set is not a fresh per-device power query; queue size is sampled, not peak backlog." };
    }
}
