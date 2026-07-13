using System.Linq;
using System.Numerics;
using Content.Server._Forge.Horizon.Components;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Horizon;
using Content.Shared._Forge.Horizon.Prototypes;
using Robust.Shared.Map;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem
{
    partial void OnStrategicCycle()
    {
        ProcessNextBuildOrder();
        ProcessPendingIncidents();
    }

    private void ProcessNextBuildOrder()
    {
        var order = State.Orders.Values
            .Where(candidate => candidate.Status == HorizonOrderStatus.Queued &&
                                candidate.Type == HorizonOrderType.DeployStation)
            .OrderBy(candidate => candidate.CreatedAt)
            .FirstOrDefault();
        if (order is null || !_prototypes.TryIndex<HorizonProjectPrototype>(order.ProjectId, out var project))
            return;

        if (!TryGetClusterAnchor(out var anchorEntity, out var mapId, out var anchor))
        {
            order.Status = HorizonOrderStatus.Failed;
            order.FailureReason = "active cluster has no anchor";
            return;
        }

        var objectLimit = Math.Max(2, _configuration.GetCVar(ForgeCVars.HorizonSpatialObjectLimit));
        var objects = State.Objects.Values
            .Where(obj => obj.Active && obj.MapId == mapId && obj.Kind != HorizonObjectKind.Ams)
            .Take(objectLimit)
            .Select(obj => new HorizonSpatialObject(obj.WorldPosition, obj.BranchDepth))
            .ToArray();
        var zones = State.ProtectedZones.Take(objectLimit).ToArray();
        var placement = HorizonSpatialPolicy.FindPlacement(
            anchor,
            objects,
            zones,
            mapId,
            project.MinDistance,
            project.PreferredDistance,
            project.MaxDistance,
            project.ProtectedRadius,
            _configuration.GetCVar(ForgeCVars.HorizonBubbleRadius),
            _configuration.GetCVar(ForgeCVars.HorizonMaxBranchDepth),
            _configuration.GetCVar(ForgeCVars.HorizonSpatialCandidateCount));
        State.Performance.CandidateCount = Math.Min(
            Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonSpatialCandidateCount)),
            64);
        if (placement is not { } selected)
        {
            order.Status = HorizonOrderStatus.Failed;
            order.FailureReason = "bounded spatial search found no valid candidate";
            return;
        }

        order.Status = HorizonOrderStatus.Active;
        order.StartedAt = _timing.CurTime;
        order.TargetCoordinates = new EntityCoordinates(anchorEntity, selected.Position - anchor);
        if (!HorizonEconomy.TrySpend(
                State.Ledger,
                project.RawCost,
                project.ComponentCost,
                project.EnergyCost))
        {
            order.Status = HorizonOrderStatus.Queued;
            order.StartedAt = null;
            return;
        }

        if (!TryLoadProjectGrid(project.ID, selected.Position, out var grid) || grid is not { } stationGrid)
        {
            HorizonEconomy.Refund(State.Ledger, project.RawCost, project.ComponentCost, project.EnergyCost,
                _configuration.GetCVar(ForgeCVars.HorizonResourceCap));
            order.Status = HorizonOrderStatus.Failed;
            order.FailureReason = "project grid could not be loaded";
            return;
        }

        var stationCore = project.Kind == HorizonObjectKind.Amz
            ? CreateAmzExecutor(stationGrid)
            : Spawn("HorizonStationCore", new EntityCoordinates(stationGrid, Vector2.Zero));
        ConfigureObject(
            stationCore,
            project.ObjectId,
            project.Kind,
            project.ID,
            State.ActiveCluster,
            true,
            false,
            project.RawIncome,
            project.EnergyCapacity,
            project.ProductionCapacity,
            project.ProtectedRadius,
            selected.BranchDepth,
            project.TemporaryContent);
        _metadata.SetEntityName(stationGrid, project.Name);
        order.Executor = stationCore;
        order.Status = HorizonOrderStatus.Complete;
        AnnounceOnce($"project-{project.ID}",
            Loc.GetString("horizon-announcement-project-online", ("project", project.ObjectId)));
    }

    private EntityUid CreateAmzExecutor(EntityUid grid)
    {
        var core = TryFindShuttleConsole(grid, out var existing)
            ? existing
            : Spawn("ComputerShuttle", new EntityCoordinates(grid, Vector2.Zero));
        EnsureComp<HorizonObjectComponent>(core);
        EnsureComp<HorizonDefenseExecutorComponent>(core);
        _npcFaction.AddFaction(core, "Horizon");
        _shuttle.SetIFFColor(grid, Color.FromHex("#35c9c2"));
        _shuttle.SetIFFReadOnly(grid, true);
        return core;
    }

    private bool TryGetClusterAnchor(out EntityUid anchorEntity, out MapId mapId, out Vector2 position)
    {
        anchorEntity = EntityUid.Invalid;
        mapId = MapId.Nullspace;
        position = default;

        if (State.CommandObject is { } command && State.Objects.TryGetValue(command, out var commandRecord))
        {
            anchorEntity = command;
            mapId = commandRecord.MapId;
            position = commandRecord.WorldPosition;
            return true;
        }

        if (State.PrimaryRtr is not { } primary || State.NeighborRtr is not { } neighbor ||
            !State.Objects.TryGetValue(primary, out var primaryRecord) ||
            !State.Objects.TryGetValue(neighbor, out var neighborRecord))
        {
            return false;
        }

        anchorEntity = primary;
        mapId = primaryRecord.MapId;
        position = (primaryRecord.WorldPosition + neighborRecord.WorldPosition) / 2f;
        return true;
    }

    public bool RegisterProtectedZone(MapId mapId, Vector2 position, float radius, bool hard)
    {
        var limit = Math.Max(2, _configuration.GetCVar(ForgeCVars.HorizonSpatialObjectLimit));
        if (State.ProtectedZones.Count >= limit)
            return false;

        State.ProtectedZones.Add(new HorizonProtectedZone(mapId, position, Math.Max(0f, radius), hard, null));
        return true;
    }
}
