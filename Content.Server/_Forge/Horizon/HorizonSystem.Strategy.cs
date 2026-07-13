using System.Diagnostics;
using System.Linq;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Horizon;
using Content.Shared._Forge.Horizon.Prototypes;
using Robust.Shared.Map;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem
{
    private TimeSpan _nextStrategicCycle;

    private void ResetStrategyState()
    {
        _nextStrategicCycle = default;
    }

    private void UpdateStrategy()
    {
        if (!_roundInitialized ||
            State.Phase is not (HorizonDeploymentPhase.Operational or HorizonDeploymentPhase.Degraded) ||
            _timing.CurTime < _nextStrategicCycle)
        {
            return;
        }

        _nextStrategicCycle = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(1f, _configuration.GetCVar(ForgeCVars.HorizonStrategicInterval)));
        if (!State.WorkQueue.TryEnqueue(new HorizonWorkItem(HorizonWorkKind.RunStrategicCycle, _timing.CurTime)))
            State.Performance.DeferredJobs++;
    }

    private void RunStrategicCycle()
    {
        if (State.Phase is not (HorizonDeploymentPhase.Operational or HorizonDeploymentPhase.Degraded))
            return;

        var stopwatch = Stopwatch.StartNew();
        HorizonEconomy.ApplyCycle(
            State.Ledger,
            State.Aggregates,
            _configuration.GetCVar(ForgeCVars.HorizonResourceCap));
        PruneFinishedOrders();
        TryPlanNextProject();
        stopwatch.Stop();
        State.Performance.LastStrategicMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        if (State.Performance.LastStrategicMilliseconds > State.Performance.LongestStepMilliseconds)
        {
            State.Performance.LongestStepMilliseconds = State.Performance.LastStrategicMilliseconds;
            State.Performance.LongestStep = "strategic-cycle";
        }
    }

    private void TryPlanNextProject()
    {
        if (ActiveOrderCount() >= Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonMaxOrders)))
            return;

        var candidates = _prototypes.EnumeratePrototypes<HorizonProjectPrototype>()
            .Select(project => new HorizonProjectCandidate(
                project.ID,
                project.Kind,
                project.Priority,
                project.DesiredCount,
                project.MaxCount,
                project.RawCost,
                project.ComponentCost,
                project.EnergyCost));
        var selected = HorizonPlanningPolicy.SelectNext(candidates, State.ObjectCounts, State.Ledger);
        if (selected is not { } project)
            return;

        TryCreateOrder(HorizonOrderType.DeployStation, project.ProjectId, null, null, out _);
    }

    public bool TryCreateOrder(
        HorizonOrderType type,
        string projectId,
        EntityUid? targetEntity,
        EntityCoordinates? targetCoordinates,
        out Guid orderId)
    {
        orderId = default;
        PruneFinishedOrders();
        var maxOrders = Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonMaxOrders));
        if (ActiveOrderCount() >= maxOrders || State.Orders.Count >= maxOrders)
            return false;

        var order = new HorizonOrder
        {
            Type = type,
            ProjectId = projectId,
            TargetEntity = targetEntity,
            TargetCoordinates = targetCoordinates,
            CreatedAt = _timing.CurTime,
            Deadline = _timing.CurTime + TimeSpan.FromSeconds(
                Math.Max(1f, _configuration.GetCVar(ForgeCVars.HorizonAmsMoveTimeout))),
        };
        State.Orders.Add(order.Id, order);
        orderId = order.Id;
        return true;
    }

    public bool SetOrderStatus(Guid orderId, HorizonOrderStatus status, string failureReason = "")
    {
        if (!State.Orders.TryGetValue(orderId, out var order))
            return false;

        order.Status = status;
        order.FailureReason = failureReason;
        if (status == HorizonOrderStatus.Active && order.StartedAt is null)
            order.StartedAt = _timing.CurTime;
        return true;
    }

    private int ActiveOrderCount()
    {
        return State.Orders.Values.Count(order =>
            order.Status is HorizonOrderStatus.Queued or HorizonOrderStatus.Active);
    }

    private void PruneFinishedOrders()
    {
        var maxOrders = Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonMaxOrders));
        if (State.Orders.Count < maxOrders)
            return;

        var removeCount = State.Orders.Count - maxOrders + 1;
        foreach (var order in State.Orders.Values
                     .Where(order => order.Status is not (HorizonOrderStatus.Queued or HorizonOrderStatus.Active))
                     .OrderBy(order => order.CreatedAt)
                     .Take(removeCount)
                     .ToArray())
        {
            State.Orders.Remove(order.Id);
        }
    }
}
