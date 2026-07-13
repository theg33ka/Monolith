using System.Text;
using System.Linq;
using Content.Server.Administration;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Forge.Horizon.Commands;

internal static class HorizonCommandOutput
{
    public static HorizonSystem GetSystem(IEntitySystemManager systems) => systems.GetEntitySystem<HorizonSystem>();

    public static string Status(HorizonState state)
    {
        return $"phase={state.Phase} cluster={Value(state.ActiveCluster)} objects={state.Objects.Count} " +
               $"orders={state.Orders.Count} incidents={state.Incidents.Count} queue={state.WorkQueue.Count}/{state.WorkQueue.Capacity} " +
               $"raw={state.Ledger.Raw} components={state.Ledger.Components} energy={state.Ledger.Energy} " +
               $"income={state.Aggregates.RawIncome} production={state.Aggregates.ProductionCapacity} " +
               $"ams_attempt={state.AmsAttempt}/3 emergency_used={state.EmergencyClusterUsed} mature={state.MatureNetwork}";
    }

    public static string Objects(HorizonState state)
    {
        if (state.Objects.Count == 0)
            return "No registered Horizon objects.";

        var output = new StringBuilder();
        foreach (var obj in state.Objects.Values.OrderBy(o => o.ObjectId, StringComparer.OrdinalIgnoreCase))
        {
            output.AppendLine($"{obj.ObjectId}: uid={obj.Entity} grid={obj.Grid?.ToString() ?? "none"} kind={obj.Kind} " +
                              $"active={obj.Active} dormant={obj.Dormant} cluster={Value(obj.ClusterId)} " +
                              $"pos=({obj.WorldPosition.X:F0},{obj.WorldPosition.Y:F0}) branch={obj.BranchDepth}");
        }

        return output.ToString().TrimEnd();
    }

    public static string Orders(HorizonState state)
    {
        if (state.Orders.Count == 0)
            return "No Horizon orders.";

        return string.Join(Environment.NewLine, state.Orders.Values.OrderBy(o => o.CreatedAt).Select(order =>
            $"{order.Id}: {order.Type} {order.Status} executor={order.Executor?.ToString() ?? "none"} " +
            $"project={Value(order.ProjectId)} deadline={order.Deadline} reason={Value(order.FailureReason)}"));
    }

    public static string Incidents(HorizonState state)
    {
        if (state.Incidents.Count == 0)
            return "No Horizon incidents.";

        return string.Join(Environment.NewLine, state.Incidents.Values.OrderByDescending(i => i.LastSeen).Select(incident =>
            $"{incident.Key}: org={incident.Organization} damage={incident.Damage:F1} destroyed={incident.DestroyedObjects} " +
            $"target={incident.Target?.ToString() ?? "none"} response={incident.ResponseOrdered}"));
    }

    public static string Relations(HorizonState state)
    {
        if (state.Relations.Count == 0)
            return "No Horizon relations.";

        return string.Join(Environment.NewLine, state.Relations.Values.OrderBy(r => r.Organization).Select(relation =>
            $"{relation.Organization}: contribution={relation.Contribution} damage={relation.Damage} " +
            $"access={relation.Access} iff={relation.Iff}"));
    }

    public static string Performance(HorizonState state)
    {
        var perf = state.Performance;
        return $"strategic_ms={perf.LastStrategicMilliseconds:F2} longest={perf.LongestStep}:{perf.LongestStepMilliseconds:F2}ms " +
               $"queue={state.WorkQueue.Count}/{state.WorkQueue.Capacity} peak={perf.QueuePeak} rejected={state.WorkQueue.Rejected} " +
               $"objects={state.Objects.Count} orders={state.Orders.Count} incidents={state.Incidents.Count} " +
               $"candidates={perf.CandidateCount} spawn_ms={perf.LastGridSpawnMilliseconds:F2} " +
               $"replace_ms={perf.LastGridReplaceMilliseconds:F2} deferred={perf.DeferredJobs}";
    }

    private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "none" : value;
}

[AdminCommand(AdminFlags.Debug)]
public sealed class HorizonStatusCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    public string Command => "horizon_status";
    public string Description => "Shows the АКС Horizon strategic state.";
    public string Help => Command;
    public void Execute(IConsoleShell shell, string argStr, string[] args) =>
        shell.WriteLine(HorizonCommandOutput.Status(HorizonCommandOutput.GetSystem(_systems).State));
}

[AdminCommand(AdminFlags.Debug)]
public sealed class HorizonObjectsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    public string Command => "horizon_objects";
    public string Description => "Shows lifecycle-registered АКС Horizon objects.";
    public string Help => Command;
    public void Execute(IConsoleShell shell, string argStr, string[] args) =>
        shell.WriteLine(HorizonCommandOutput.Objects(HorizonCommandOutput.GetSystem(_systems).State));
}

[AdminCommand(AdminFlags.Debug)]
public sealed class HorizonOrdersCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    public string Command => "horizon_orders";
    public string Description => "Shows АКС Horizon strategic orders.";
    public string Help => Command;
    public void Execute(IConsoleShell shell, string argStr, string[] args) =>
        shell.WriteLine(HorizonCommandOutput.Orders(HorizonCommandOutput.GetSystem(_systems).State));
}

[AdminCommand(AdminFlags.Debug)]
public sealed class HorizonIncidentsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    public string Command => "horizon_incidents";
    public string Description => "Shows aggregated АКС Horizon incidents.";
    public string Help => Command;
    public void Execute(IConsoleShell shell, string argStr, string[] args) =>
        shell.WriteLine(HorizonCommandOutput.Incidents(HorizonCommandOutput.GetSystem(_systems).State));
}

[AdminCommand(AdminFlags.Debug)]
public sealed class HorizonRelationsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    public string Command => "horizon_relations";
    public string Description => "Shows АКС Horizon organization relations.";
    public string Help => Command;
    public void Execute(IConsoleShell shell, string argStr, string[] args) =>
        shell.WriteLine(HorizonCommandOutput.Relations(HorizonCommandOutput.GetSystem(_systems).State));
}

[AdminCommand(AdminFlags.Debug)]
public sealed class HorizonPerformanceCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    public string Command => "horizon_perf";
    public string Description => "Shows bounded scheduler and spawn performance metrics.";
    public string Help => Command;
    public void Execute(IConsoleShell shell, string argStr, string[] args) =>
        shell.WriteLine(HorizonCommandOutput.Performance(HorizonCommandOutput.GetSystem(_systems).State));
}

[AdminCommand(AdminFlags.Debug)]
public sealed class HorizonForceEventCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "horizon_force_event";
    public string Description => "Forces a bounded АКС Horizon lifecycle event for testing.";
    public string Help => "horizon_force_event <setup|activate|auto|destroy> [RTR entity]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError(Help);
            return;
        }

        var system = HorizonCommandOutput.GetSystem(_systems);
        switch (args[0].ToLowerInvariant())
        {
            case "setup":
                shell.WriteLine(system.SetupRound());
                break;
            case "activate":
            {
                EntityUid? requested = null;
                if (args.Length > 1)
                {
                    if (!EntityUid.TryParse(args[1], out var parsed))
                    {
                        shell.WriteError($"Invalid RTR entity uid: {args[1]}");
                        return;
                    }
                    requested = parsed;
                }
                shell.WriteLine(system.BeginActivation(requested, automatic: false));
                break;
            }
            case "auto":
                shell.WriteLine(system.BeginActivation(null, automatic: true));
                break;
            case "destroy":
                system.DestroyNetwork("admin force event");
                shell.WriteLine("Horizon network marked permanently destroyed.");
                break;
            default:
                shell.WriteError(Help);
                break;
        }
    }
}
