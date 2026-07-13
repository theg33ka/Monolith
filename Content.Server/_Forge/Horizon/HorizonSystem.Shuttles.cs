using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.Server._Forge.Horizon.Components;
using Content.Server._Forge.Horizon.Domain;
using Content.Server._Mono.NPC.HTN.Operators;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Horizon;
using Content.Shared._Forge.Horizon.Components;
using Content.Shared._Forge.Horizon.Prototypes;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly HashSet<EntityUid> _shuttleCores = new();
    private TimeSpan _nextOrderCheck;

    private void InitializeShuttles()
    {
        SubscribeLocalEvent<HorizonShuttleCoreComponent, ComponentStartup>(OnShuttleCoreStartup);
        SubscribeLocalEvent<HorizonShuttleCoreComponent, ComponentShutdown>(OnShuttleCoreShutdown);
        SubscribeLocalEvent<HorizonShuttleCoreComponent, SteeringDoneEvent>(OnShuttleSteeringDone);
    }

    private void ResetShuttleState()
    {
        _shuttleCores.Clear();
        _nextOrderCheck = default;
    }

    private void OnShuttleCoreStartup(Entity<HorizonShuttleCoreComponent> ent, ref ComponentStartup args)
    {
        _shuttleCores.Add(ent.Owner);
    }

    private void OnShuttleCoreShutdown(Entity<HorizonShuttleCoreComponent> ent, ref ComponentShutdown args)
    {
        _shuttleCores.Remove(ent.Owner);
        if (State.ActiveAms != ent.Owner || State.SuppressAmsFailure || State.Phase == HorizonDeploymentPhase.Destroyed)
            return;

        HandleAmsFailure(ent.Owner, "AMS executor was destroyed");
    }

    private void OnShuttleSteeringDone(Entity<HorizonShuttleCoreComponent> ent, ref SteeringDoneEvent args)
    {
        if (ent.Comp.OrderId is not { } orderId ||
            !State.Orders.TryGetValue(orderId, out var order) ||
            order.Status != HorizonOrderStatus.Active ||
            State.ActiveAms != ent.Owner)
        {
            return;
        }

        ent.Comp.Deploying = true;
        ent.Comp.DeployAt = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(0f, _configuration.GetCVar(ForgeCVars.HorizonDeploySeconds)));
        AnnounceOnce("ams-arrived", Loc.GetString("horizon-announcement-ams-arrived"));
    }

    partial void OnInitialClusterReady()
    {
        QueueAmsSpawn(TimeSpan.Zero);
    }

    private void UpdateShuttles()
    {
        if (!_roundInitialized || State.Phase == HorizonDeploymentPhase.Destroyed || _timing.CurTime < _nextOrderCheck)
            return;

        _nextOrderCheck = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(0.25f, _configuration.GetCVar(ForgeCVars.HorizonOrderCheckInterval)));

        var now = _timing.CurTime;
        var limit = Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonWorkItemsPerTick));
        State.WorkQueue.Drain(limit, item => ProcessShuttleWork(item, now));
        State.Performance.QueuePeak = Math.Max(State.Performance.QueuePeak, State.WorkQueue.Count);

        foreach (var core in _shuttleCores.ToArray())
        {
            if (Deleted(core) || !TryComp<HorizonShuttleCoreComponent>(core, out var shuttle))
                continue;

            if (shuttle.Deploying && now >= shuttle.DeployAt)
            {
                shuttle.Deploying = false;
                CompleteO01Deployment(core, shuttle);
                break;
            }

            if (shuttle.OrderId is not { } orderId || !State.Orders.TryGetValue(orderId, out var order))
                continue;

            if (order.Status == HorizonOrderStatus.Active && now >= order.Deadline)
            {
                order.Status = HorizonOrderStatus.TimedOut;
                order.FailureReason = "movement or deployment timeout";
                HandleAmsFailure(core, order.FailureReason);
                DeleteAmsGrid(core);
                break;
            }
        }
    }

    private void ProcessShuttleWork(HorizonWorkItem item, TimeSpan now)
    {
        if (item.NotBefore > now)
        {
            if (!State.WorkQueue.TryEnqueue(item))
                State.Performance.DeferredJobs++;
            return;
        }

        switch (item.Kind)
        {
            case HorizonWorkKind.SpawnAms:
                SpawnAms();
                break;
            case HorizonWorkKind.CompleteDeployment when item.Entity is { } core &&
                                                         TryComp<HorizonShuttleCoreComponent>(core, out var shuttle):
                CompleteO01Deployment(core, shuttle);
                break;
            case HorizonWorkKind.RunStrategicCycle:
                RunStrategicCycle();
                break;
        }
    }

    private void QueueAmsSpawn(TimeSpan delay)
    {
        if (!State.WorkQueue.TryEnqueue(new HorizonWorkItem(
                HorizonWorkKind.SpawnAms,
                _timing.CurTime + delay)))
        {
            DestroyNetwork("AMS recovery queue overflow");
        }
    }

    private void SpawnAms()
    {
        if (State.Phase != HorizonDeploymentPhase.Deploying || State.PrimaryRtr is not { } primary ||
            State.NeighborRtr is not { } neighbor || !State.Objects.ContainsKey(primary) || !State.Objects.ContainsKey(neighbor))
        {
            return;
        }

        var primaryRecord = State.Objects[primary];
        var neighborRecord = State.Objects[neighbor];
        var target = (primaryRecord.WorldPosition + neighborRecord.WorldPosition) / 2f;
        var direction = target - primaryRecord.WorldPosition;
        var spawnOffset = primaryRecord.WorldPosition + (direction.LengthSquared() > 0f ? Vector2.Normalize(direction) * 120f : Vector2.Zero);

        if (!TryLoadProjectGrid("HorizonAMS01", spawnOffset, out var grid) ||
            !TryFindShuttleConsole(grid, out var core))
        {
            if (grid is { } failedGrid)
                QueueDel(failedGrid);
            HandleAmsFailure(EntityUid.Invalid, "AMS grid or shuttle console could not be loaded");
            return;
        }

        var project = _prototypes.Index<HorizonProjectPrototype>("HorizonAMS01");
        EnsureComp<HorizonObjectComponent>(core);
        EnsureComp<HorizonShuttleCoreComponent>(core);
        ConfigureObject(
            core,
            $"AMS-01-{State.AmsAttempt + 1}",
            HorizonObjectKind.Ams,
            project.ID,
            State.ActiveCluster,
            true,
            false,
            project.RawIncome,
            project.EnergyCapacity,
            project.ProductionCapacity,
            project.ProtectedRadius,
            0,
            project.TemporaryContent);

        State.AmsAttempt++;
        State.ActiveAms = core;
        var order = new HorizonOrder
        {
            Type = HorizonOrderType.DeployStation,
            Status = HorizonOrderStatus.Active,
            Executor = core,
            TargetCoordinates = new EntityCoordinates(primary, target - primaryRecord.WorldPosition),
            ProjectId = "HorizonO01",
            CreatedAt = _timing.CurTime,
            StartedAt = _timing.CurTime,
            Deadline = _timing.CurTime + TimeSpan.FromSeconds(
                Math.Max(1f, _configuration.GetCVar(ForgeCVars.HorizonAmsMoveTimeout)) +
                Math.Max(0f, _configuration.GetCVar(ForgeCVars.HorizonDeploySeconds))),
        };
        State.Orders[order.Id] = order;

        var shuttleCore = Comp<HorizonShuttleCoreComponent>(core);
        shuttleCore.OrderId = order.Id;
        shuttleCore.DeployProjectId = order.ProjectId;

        if (!TryComp<HTNComponent>(core, out var htn))
        {
            order.Status = HorizonOrderStatus.Failed;
            order.FailureReason = "shuttle console has no HTN autopilot";
            HandleAmsFailure(core, order.FailureReason);
            DeleteAmsGrid(core);
            return;
        }

        htn.Blackboard.SetValue("Target", _transform.ToCoordinates(new MapCoordinates(target, primaryRecord.MapId)));
        htn.Blackboard.SetValue("TargetRotation", Angle.Zero);
        _npc.WakeNPC(core, htn);
        AnnounceOnce($"ams-launch-{State.ActiveCluster}-{State.AmsAttempt}",
            Loc.GetString("horizon-announcement-ams-launched", ("attempt", State.AmsAttempt)));
    }

    private void CompleteO01Deployment(EntityUid core, HorizonShuttleCoreComponent shuttle)
    {
        if (State.ActiveAms != core || shuttle.OrderId is not { } orderId ||
            !State.Orders.TryGetValue(orderId, out var order) || order.Status != HorizonOrderStatus.Active)
        {
            return;
        }

        RefreshObjectPosition(core);
        var position = State.Objects.TryGetValue(core, out var ams)
            ? ams.WorldPosition
            : _transform.GetWorldPosition(Transform(core));

        if (!TryCreateO01(position, out var stationCore))
        {
            order.Status = HorizonOrderStatus.Failed;
            order.FailureReason = "O-01 grid could not be loaded";
            HandleAmsFailure(core, order.FailureReason);
            DeleteAmsGrid(core);
            return;
        }

        State.CommandObject = stationCore;
        State.MatureNetwork = true;
        State.Phase = HorizonDeploymentPhase.Operational;
        State.Ledger.Raw = Math.Max(State.Ledger.Raw, 250);
        State.Ledger.Components = Math.Max(State.Ledger.Components, 100);
        State.Ledger.Energy = Math.Max(State.Ledger.Energy, 200);
        order.Status = HorizonOrderStatus.Complete;
        State.SuppressAmsFailure = true;
        State.ActiveAms = null;
        DeleteAmsGrid(core);
        State.SuppressAmsFailure = false;
        AnnounceOnce("o01-online", Loc.GetString("horizon-announcement-o01-online"));
    }

    private void HandleAmsFailure(EntityUid core, string reason)
    {
        if (State.Phase == HorizonDeploymentPhase.Destroyed)
            return;

        if (core.IsValid() && State.Objects.ContainsKey(core))
            RefreshObjectPosition(core);
        State.ActiveAms = null;

        var action = HorizonRecoveryPolicy.Select(State.AmsAttempt, State.EmergencyClusterUsed);
        switch (action)
        {
            case HorizonRecoveryAction.RetryAms:
                AnnounceOnce($"ams-retry-{State.ActiveCluster}-{State.AmsAttempt}",
                    Loc.GetString("horizon-announcement-ams-retry", ("attempt", State.AmsAttempt + 1)));
                QueueAmsSpawn(TimeSpan.FromSeconds(Math.Max(0f, _configuration.GetCVar(ForgeCVars.HorizonRespawnDelay))));
                break;
            case HorizonRecoveryAction.RelocateCluster:
                if (!TryRelocateEmergencyCluster())
                {
                    DestroyNetwork($"{reason}; emergency RTR pair unavailable");
                    return;
                }

                State.EmergencyClusterUsed = true;
                State.AmsAttempt = 0;
                AnnounceOnce("emergency-relocation", Loc.GetString("horizon-announcement-emergency-relocation"));
                QueueAmsSpawn(TimeSpan.FromSeconds(Math.Max(0f, _configuration.GetCVar(ForgeCVars.HorizonRespawnDelay))));
                break;
            case HorizonRecoveryAction.TerminateCycle:
                DestroyNetwork($"{reason}; all AMS recovery attempts exhausted");
                break;
        }
    }

    private bool TryRelocateEmergencyCluster()
    {
        var dormant = State.Objects.Values
            .Where(obj => obj.Kind == HorizonObjectKind.Rtr && obj.Dormant && !Deleted(obj.Entity))
            .Select(obj => obj.Entity)
            .ToList();
        if (dormant.Count < 2)
            return false;

        var origin = State.PrimaryRtr is { } oldPrimary && State.Objects.TryGetValue(oldPrimary, out var old)
            ? old.WorldPosition
            : Vector2.Zero;
        var primary = dormant.OrderByDescending(uid => Vector2.DistanceSquared(origin, State.Objects[uid].WorldPosition)).First();
        var neighbor = HorizonDeploymentPlanner.FindNearestNeighbor(primary, dormant, uid => State.Objects[uid].WorldPosition);
        if (neighbor is null)
            return false;

        if (State.PrimaryRtr is { } previousPrimary && !Deleted(previousPrimary))
            SetObjectActivation(previousPrimary, false, true, string.Empty);
        if (State.NeighborRtr is { } previousNeighbor && !Deleted(previousNeighbor))
            SetObjectActivation(previousNeighbor, false, true, string.Empty);

        State.PrimaryRtr = primary;
        State.NeighborRtr = neighbor.Value;
        State.ActiveCluster = "HZ-02";
        State.Phase = HorizonDeploymentPhase.Deploying;
        SetObjectActivation(primary, true, false, State.ActiveCluster);
        SetObjectActivation(neighbor.Value, true, false, State.ActiveCluster);
        return true;
    }

    public string BeginLateDeployment()
    {
        if (!_roundInitialized)
        {
            var setup = SetupRound();
            if (!_roundInitialized)
                return setup;
        }

        if (State.Phase != HorizonDeploymentPhase.Dormant)
            return $"Late deployment is unavailable in phase {State.Phase}.";

        var activation = BeginActivation(null, automatic: true);
        if (State.PrimaryRtr is not { } primary || State.NeighborRtr is not { } neighbor ||
            !State.Objects.TryGetValue(primary, out var primaryRecord) ||
            !State.Objects.TryGetValue(neighbor, out var neighborRecord))
            return activation;

        var position = (primaryRecord.WorldPosition + neighborRecord.WorldPosition) / 2f;
        if (!TryCreateO01(position, out var stationCore))
        {
            DestroyNetwork("late deployment O-01 could not be loaded");
            return "Late Horizon deployment failed to load O-01 and was terminated.";
        }

        State.WakeCompletesAt = null;
        State.CommandObject = stationCore;
        State.LateDeployment = true;
        State.MatureNetwork = true;
        State.Phase = HorizonDeploymentPhase.Operational;
        State.Ledger.Raw = 100;
        State.Ledger.Components = 40;
        State.Ledger.Energy = 80;
        AnnounceOnce("late-deployment", Loc.GetString("horizon-announcement-late-deployment"));
        return $"Late Horizon beachhead with O-01 activated on {State.ActiveCluster}.";
    }

    private bool TryCreateO01(Vector2 position, out EntityUid stationCore)
    {
        stationCore = EntityUid.Invalid;
        if (!TryLoadProjectGrid("HorizonO01", position, out var grid) || grid is not { } stationGrid)
            return false;

        var project = _prototypes.Index<HorizonProjectPrototype>("HorizonO01");
        stationCore = Spawn("HorizonStationCore", new EntityCoordinates(stationGrid, Vector2.Zero));
        ConfigureObject(
            stationCore,
            project.ObjectId,
            HorizonObjectKind.Command,
            project.ID,
            State.ActiveCluster,
            true,
            false,
            project.RawIncome,
            project.EnergyCapacity,
            project.ProductionCapacity,
            project.ProtectedRadius,
            0,
            project.TemporaryContent);
        _metadata.SetEntityName(stationGrid, "Horizon O-01");
        SpawnHorizonConsoles(stationGrid);
        SpawnHorizonProjectFixtures(stationGrid, HorizonObjectKind.Command, project.ObjectId);
        Spawn("PlayerStationAiHorizon", new EntityCoordinates(stationGrid, new Vector2(0f, 1f)));
        return true;
    }

    private bool TryLoadProjectGrid(string projectId, Vector2 position, out EntityUid? grid)
    {
        grid = null;
        if (!_prototypes.TryIndex<HorizonProjectPrototype>(projectId, out var project))
            return false;

        var stopwatch = Stopwatch.StartNew();
        var loaded = _mapLoader.TryLoadGrid(_ticker.DefaultMap, project.GridPath, out var loadedGrid, offset: position);
        grid = loadedGrid?.Owner;
        stopwatch.Stop();
        State.Performance.LastGridSpawnMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        if (State.Performance.LastGridSpawnMilliseconds > State.Performance.LongestStepMilliseconds)
        {
            State.Performance.LongestStepMilliseconds = State.Performance.LastGridSpawnMilliseconds;
            State.Performance.LongestStep = $"load:{projectId}";
        }
        return loaded;
    }

    private bool TryFindShuttleConsole(EntityUid? grid, out EntityUid console)
    {
        console = EntityUid.Invalid;
        if (grid is not { } gridUid || Deleted(gridUid))
            return false;

        var children = Transform(gridUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!HasComp<ShuttleConsoleComponent>(child))
                continue;

            console = child;
            return true;
        }

        return false;
    }

    private void DeleteAmsGrid(EntityUid core)
    {
        if (!State.Objects.TryGetValue(core, out var record))
            return;

        if (record.Grid is { } grid && !Deleted(grid))
            QueueDel(grid);
        else if (!Deleted(core))
            QueueDel(core);
    }
}
