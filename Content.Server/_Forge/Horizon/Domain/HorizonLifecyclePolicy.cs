using Content.Shared._Forge.Horizon;

namespace Content.Server._Forge.Horizon.Domain;

public static class HorizonLifecyclePolicy
{
    public static bool Destroy(HorizonState state, string reason)
    {
        if (state.Phase == HorizonDeploymentPhase.Destroyed)
            return false;

        state.Phase = HorizonDeploymentPhase.Destroyed;
        state.MatureNetwork = false;
        state.WakeCompletesAt = null;
        state.PrimaryRtr = null;
        state.NeighborRtr = null;
        state.ActiveAms = null;
        state.CommandObject = null;
        state.WorkQueue.Clear();

        foreach (var order in state.Orders.Values)
        {
            if (order.Status is not (HorizonOrderStatus.Queued or HorizonOrderStatus.Active))
                continue;

            order.Status = HorizonOrderStatus.Cancelled;
            order.FailureReason = reason;
        }

        foreach (var obj in state.Objects.Values)
        {
            obj.Active = false;
            obj.Dormant = false;
        }

        foreach (var incident in state.Incidents.Values)
            incident.ResponseOrdered = true;

        state.ProtectedZones.Clear();
        state.Aggregates.Reset();
        return true;
    }
}
