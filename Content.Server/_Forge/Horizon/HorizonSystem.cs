using System.Numerics;
using Content.Server._Forge.Horizon.Domain;
using Content.Server.GameTicking;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Horizon;
using Content.Shared._Forge.Horizon.Components;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public HorizonState State { get; } = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<HorizonObjectComponent, ComponentStartup>(OnObjectStartup);
        SubscribeLocalEvent<HorizonObjectComponent, ComponentShutdown>(OnObjectShutdown);

        State.Reset(_configuration.GetCVar(ForgeCVars.HorizonMaxWorkQueue));
        InitializeDeployment();
        InitializeShuttles();
        InitializeDefense();
        InitializeConsoles();
        InitializeWanderingAi();
        ResetStrategyState();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateDeployment();
        UpdateStrategy();
        UpdateShuttles();
        UpdateDefense();
        UpdateConsoles();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        State.Reset(_configuration.GetCVar(ForgeCVars.HorizonMaxWorkQueue));
        ResetDeploymentState();
        ResetShuttleState();
        ResetDefenseState();
        ResetConsoleState();
        ResetWanderingAiState();
        ResetStrategyState();
    }

    private void OnObjectStartup(Entity<HorizonObjectComponent> ent, ref ComponentStartup args)
    {
        RegisterObject(ent);
    }

    private void OnObjectShutdown(Entity<HorizonObjectComponent> ent, ref ComponentShutdown args)
    {
        var commandDestroyed = State.CommandObject == ent.Owner && State.Phase != HorizonDeploymentPhase.Destroyed;
        OnRegisteredObjectDestroyed(ent.Owner);
        UnregisterObject(ent.Owner);
        if (commandDestroyed)
            DestroyNetwork("O-01 command object was destroyed");
    }

    public bool RegisterObject(Entity<HorizonObjectComponent> ent)
    {
        if (State.Objects.ContainsKey(ent.Owner))
            return false;

        var xform = Transform(ent.Owner);
        var objectId = string.IsNullOrWhiteSpace(ent.Comp.ObjectId)
            ? $"{ent.Comp.Kind}-{ent.Owner}"
            : ent.Comp.ObjectId;

        if (State.ObjectsById.ContainsKey(objectId))
            objectId = $"{objectId}-{ent.Owner}";

        var record = new HorizonRegisteredObject
        {
            Entity = ent.Owner,
            Grid = xform.GridUid,
            ObjectId = objectId,
            Kind = ent.Comp.Kind,
            ProjectId = ent.Comp.ProjectId,
            ClusterId = ent.Comp.ClusterId,
            MapId = xform.MapID,
            WorldPosition = _transform.GetWorldPosition(xform),
            Dormant = ent.Comp.Dormant,
            Active = ent.Comp.Active && !ent.Comp.Dormant,
            RawIncome = ent.Comp.RawIncome,
            EnergyCapacity = ent.Comp.EnergyCapacity,
            ProductionCapacity = ent.Comp.ProductionCapacity,
            ProtectedRadius = ent.Comp.ProtectedRadius,
            BranchDepth = ent.Comp.BranchDepth,
            TemporaryContent = ent.Comp.TemporaryContent,
        };

        State.Objects.Add(ent.Owner, record);
        State.ObjectsById.Add(objectId, ent.Owner);
        State.ObjectCounts[record.Kind] = State.ObjectCounts.GetValueOrDefault(record.Kind) + 1;
        State.Aggregates.Add(record, 1);

        if (record.Active && record.ProtectedRadius > 0f)
            State.ProtectedZones.Add(new HorizonProtectedZone(record.MapId, record.WorldPosition, record.ProtectedRadius, true, ent.Owner));

        return true;
    }

    public bool UnregisterObject(EntityUid uid)
    {
        if (!State.Objects.Remove(uid, out var record))
            return false;

        State.ObjectsById.Remove(record.ObjectId);
        State.Aggregates.Add(record, -1);

        var count = Math.Max(0, State.ObjectCounts.GetValueOrDefault(record.Kind) - 1);
        if (count == 0)
            State.ObjectCounts.Remove(record.Kind);
        else
            State.ObjectCounts[record.Kind] = count;

        State.ProtectedZones.RemoveAll(zone => zone.Entity == uid);
        return true;
    }

    public void RefreshObjectPosition(EntityUid uid)
    {
        if (!State.Objects.TryGetValue(uid, out var record) || Deleted(uid))
            return;

        var xform = Transform(uid);
        record.Grid = xform.GridUid;
        record.MapId = xform.MapID;
        record.WorldPosition = _transform.GetWorldPosition(xform);
    }

    public bool SetObjectActivation(EntityUid uid, bool active, bool dormant, string clusterId)
    {
        if (!State.Objects.TryGetValue(uid, out var record) || !TryComp<HorizonObjectComponent>(uid, out var component))
            return false;

        State.Aggregates.Add(record, -1);
        State.ProtectedZones.RemoveAll(zone => zone.Entity == uid);

        component.Active = active;
        component.Dormant = dormant;
        component.ClusterId = clusterId;
        record.Active = active && !dormant;
        record.Dormant = dormant;
        record.ClusterId = clusterId;

        State.Aggregates.Add(record, 1);
        if (record.Active && record.ProtectedRadius > 0f)
            State.ProtectedZones.Add(new HorizonProtectedZone(record.MapId, record.WorldPosition, record.ProtectedRadius, true, uid));

        return true;
    }

    public bool ConfigureObject(
        EntityUid uid,
        string objectId,
        HorizonObjectKind kind,
        string projectId,
        string clusterId,
        bool active,
        bool dormant,
        int rawIncome,
        int energyCapacity,
        int productionCapacity,
        float protectedRadius,
        int branchDepth,
        bool temporaryContent)
    {
        if (!State.Objects.TryGetValue(uid, out var record) || !TryComp<HorizonObjectComponent>(uid, out var component))
            return false;

        State.Aggregates.Add(record, -1);
        State.ProtectedZones.RemoveAll(zone => zone.Entity == uid);
        State.ObjectsById.Remove(record.ObjectId);

        if (State.ObjectsById.ContainsKey(objectId))
            objectId = $"{objectId}-{uid}";

        var oldKind = record.Kind;
        var oldCount = Math.Max(0, State.ObjectCounts.GetValueOrDefault(oldKind) - 1);
        if (oldCount == 0)
            State.ObjectCounts.Remove(oldKind);
        else
            State.ObjectCounts[oldKind] = oldCount;

        component.ObjectId = objectId;
        component.Kind = kind;
        component.ProjectId = projectId;
        component.ClusterId = clusterId;
        component.Active = active;
        component.Dormant = dormant;
        component.RawIncome = rawIncome;
        component.EnergyCapacity = energyCapacity;
        component.ProductionCapacity = productionCapacity;
        component.ProtectedRadius = protectedRadius;
        component.BranchDepth = branchDepth;
        component.TemporaryContent = temporaryContent;

        record.ObjectId = objectId;
        record.Kind = kind;
        record.ProjectId = projectId;
        record.ClusterId = clusterId;
        record.Active = active && !dormant;
        record.Dormant = dormant;
        record.RawIncome = rawIncome;
        record.EnergyCapacity = energyCapacity;
        record.ProductionCapacity = productionCapacity;
        record.ProtectedRadius = protectedRadius;
        record.BranchDepth = branchDepth;
        record.TemporaryContent = temporaryContent;
        State.ObjectsById[objectId] = uid;
        State.ObjectCounts[kind] = State.ObjectCounts.GetValueOrDefault(kind) + 1;
        State.Aggregates.Add(record, 1);

        if (record.Active && record.ProtectedRadius > 0f)
            State.ProtectedZones.Add(new HorizonProtectedZone(record.MapId, record.WorldPosition, record.ProtectedRadius, true, uid));

        return true;
    }
}
