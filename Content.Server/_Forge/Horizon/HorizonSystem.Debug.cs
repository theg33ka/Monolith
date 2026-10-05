using System.Linq;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Horizon;
using Content.Shared._Forge.Horizon.Prototypes;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem
{
    public string DebugProjects() => string.Join(Environment.NewLine,
        _prototypes.EnumeratePrototypes<HorizonProjectPrototype>().OrderBy(project => project.ID).Select(project =>
            $"{project.ID}: {project.Kind} map={project.GridPath} desired={project.DesiredCount} max={project.MaxCount} " +
            $"cost={project.RawCost}/{project.ComponentCost}/{project.EnergyCost} temporary={project.TemporaryContent}"));

    public string DebugSetResources(int raw, int components, int energy)
    {
        var cap = Math.Max(0, _configuration.GetCVar(ForgeCVars.HorizonResourceCap));
        State.Ledger.Raw = Math.Clamp(raw, 0, cap);
        State.Ledger.Components = Math.Clamp(components, 0, cap);
        State.Ledger.Energy = Math.Clamp(energy, 0, Math.Min(cap, Math.Max(0, State.Aggregates.EnergyCapacity)));
        UpdateAllConsoleUis();
        return $"Resources set to {State.Ledger.Raw}/{State.Ledger.Components}/{State.Ledger.Energy}.";
    }

    public string DebugBuild(string projectId)
    {
        if (State.Phase is not (HorizonDeploymentPhase.Operational or HorizonDeploymentPhase.Degraded))
            return "Build requires an operational network; use horizon_force_event late first.";
        return TryCreateOrder(HorizonOrderType.DeployStation, projectId, null, null, out var order)
            ? $"Queued {projectId}: {order}. Normal costs, placement and limits apply."
            : "Build rejected: unknown/non-buildable project, project limit or full order queue.";
    }

    public string DebugPause(bool paused)
    {
        _strategyPaused = paused;
        return paused ? "Strategic scheduling paused; lifecycle, defense and shuttle timers remain active."
            : "Strategic scheduling resumed.";
    }

    public string DebugStep()
    {
        if (State.Phase is not (HorizonDeploymentPhase.Operational or HorizonDeploymentPhase.Degraded))
            return "Step requires an operational network.";
        return State.WorkQueue.TryEnqueue(new HorizonWorkItem(HorizonWorkKind.RunStrategicCycle, _timing.CurTime))
            ? "One strategic cycle queued." : "Work queue is full.";
    }

    public string DebugCancel(Guid id)
    {
        if (!State.Orders.TryGetValue(id, out var order) || order.Status != HorizonOrderStatus.Queued)
            return "Only queued orders can be cancelled.";
        SetOrderStatus(id, HorizonOrderStatus.Cancelled, "admin cancellation");
        return $"Cancelled {id}.";
    }

    public string DebugFailAms()
    {
        if (State.ActiveAms is not { } core || Deleted(core))
            return "No active AMS executor.";
        HandleAmsFailure(core, "admin simulated AMS failure");
        DeleteAmsGrid(core);
        return "AMS failure triggered through normal recovery policy.";
    }

    public string DebugWake()
    {
        if (State.Phase != HorizonDeploymentPhase.Waking)
            return "Network is not waking.";
        CompleteWake();
        return "Wake delay skipped; normal AMS deployment queued.";
    }
}
