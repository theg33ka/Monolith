using System.Numerics;
using Content.Shared._Forge.Horizon;
using Robust.Shared.Map;

namespace Content.Server._Forge.Horizon.Domain;

public sealed class HorizonState
{
    public HorizonDeploymentPhase Phase = HorizonDeploymentPhase.Dormant;
    public string ActiveCluster = string.Empty;
    public TimeSpan RoundStartedAt;
    public TimeSpan AutoActivationAt;
    public TimeSpan? WakeCompletesAt;
    public EntityUid? PrimaryRtr;
    public EntityUid? NeighborRtr;
    public EntityUid? CommandObject;
    public EntityUid? ActiveAms;
    public EntityUid? WanderingAi;
    public EntityUid? WanderingCarrier;
    public int AmsAttempt;
    public bool EmergencyClusterUsed;
    public bool MatureNetwork;
    public bool LateDeployment;
    public bool SuppressAmsFailure;

    public readonly Dictionary<EntityUid, HorizonRegisteredObject> Objects = new();
    public readonly Dictionary<string, EntityUid> ObjectsById = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<HorizonObjectKind, int> ObjectCounts = new();
    public readonly Dictionary<Guid, HorizonOrder> Orders = new();
    public readonly Dictionary<string, HorizonIncident> Incidents = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, HorizonRelation> Relations = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<HorizonProtectedZone> ProtectedZones = new();
    public readonly HorizonAggregates Aggregates = new();
    public readonly HorizonLedger Ledger = new();
    public readonly HorizonPerformanceMetrics Performance = new();

    public BoundedWorkQueue<HorizonWorkItem> WorkQueue { get; private set; } = new(64);

    public void Reset(int workQueueCapacity = 64)
    {
        Phase = HorizonDeploymentPhase.Dormant;
        ActiveCluster = string.Empty;
        RoundStartedAt = default;
        AutoActivationAt = default;
        WakeCompletesAt = null;
        PrimaryRtr = null;
        NeighborRtr = null;
        CommandObject = null;
        ActiveAms = null;
        WanderingAi = null;
        WanderingCarrier = null;
        AmsAttempt = 0;
        EmergencyClusterUsed = false;
        MatureNetwork = false;
        LateDeployment = false;
        SuppressAmsFailure = false;
        Objects.Clear();
        ObjectsById.Clear();
        ObjectCounts.Clear();
        Orders.Clear();
        Incidents.Clear();
        Relations.Clear();
        ProtectedZones.Clear();
        Aggregates.Reset();
        Ledger.Reset();
        Performance.Reset();
        WorkQueue = new BoundedWorkQueue<HorizonWorkItem>(Math.Clamp(workQueueCapacity, 1, 256));
    }
}

public sealed class HorizonRegisteredObject
{
    public required EntityUid Entity;
    public EntityUid? Grid;
    public required string ObjectId;
    public required HorizonObjectKind Kind;
    public string ProjectId = string.Empty;
    public string ClusterId = string.Empty;
    public MapId MapId;
    public Vector2 WorldPosition;
    public bool Dormant;
    public bool Active;
    public int RawIncome;
    public int EnergyCapacity;
    public int ProductionCapacity;
    public float ProtectedRadius;
    public int BranchDepth;
    public bool TemporaryContent;
}

public sealed class HorizonAggregates
{
    public int ActiveObjects;
    public int RawIncome;
    public int EnergyCapacity;
    public int ProductionCapacity;

    public void Add(HorizonRegisteredObject obj, int sign)
    {
        if (!obj.Active)
            return;

        ActiveObjects += sign;
        RawIncome += obj.RawIncome * sign;
        EnergyCapacity += obj.EnergyCapacity * sign;
        ProductionCapacity += obj.ProductionCapacity * sign;
    }

    public void Reset()
    {
        ActiveObjects = 0;
        RawIncome = 0;
        EnergyCapacity = 0;
        ProductionCapacity = 0;
    }
}

public sealed class HorizonLedger
{
    public int Raw;
    public int Components;
    public int Energy;

    public void Reset()
    {
        Raw = 0;
        Components = 0;
        Energy = 0;
    }
}

public sealed class HorizonOrder
{
    public Guid Id = Guid.NewGuid();
    public HorizonOrderType Type;
    public HorizonOrderStatus Status = HorizonOrderStatus.Queued;
    public EntityUid? Executor;
    public EntityUid? TargetEntity;
    public EntityCoordinates? TargetCoordinates;
    public string ProjectId = string.Empty;
    public string FailureReason = string.Empty;
    public TimeSpan CreatedAt;
    public TimeSpan? StartedAt;
    public TimeSpan Deadline;
}

public sealed class HorizonIncident
{
    public required string Key;
    public required string Organization;
    public EntityUid? Target;
    public EntityUid? Origin;
    public Vector2 Position;
    public float Damage;
    public int DestroyedObjects;
    public TimeSpan FirstSeen;
    public TimeSpan LastSeen;
    public bool ResponseOrdered;
}

public sealed class HorizonRelation
{
    public required string Organization;
    public int Contribution;
    public int Damage;
    public HorizonAccessTier Access = HorizonAccessTier.Basic;
    public HorizonIffMode Iff = HorizonIffMode.Neutral;
}

public readonly record struct HorizonProtectedZone(
    MapId MapId,
    Vector2 Position,
    float Radius,
    bool Hard,
    EntityUid? Entity);

public readonly record struct HorizonWorkItem(
    HorizonWorkKind Kind,
    TimeSpan NotBefore,
    Guid? OrderId = null,
    EntityUid? Entity = null,
    string ProjectId = "");

public enum HorizonWorkKind : byte
{
    CompleteWake,
    SpawnAms,
    CompleteDeployment,
    RunStrategicCycle,
    SpawnDefense,
    RefreshUi,
}

public sealed class HorizonPerformanceMetrics
{
    public double LastStrategicMilliseconds;
    public double LongestStepMilliseconds;
    public string LongestStep = string.Empty;
    public int QueuePeak;
    public int CandidateCount;
    public double LastGridSpawnMilliseconds;
    public double LastGridReplaceMilliseconds;
    public int DeferredJobs;

    public void Reset()
    {
        LastStrategicMilliseconds = 0;
        LongestStepMilliseconds = 0;
        LongestStep = string.Empty;
        QueuePeak = 0;
        CandidateCount = 0;
        LastGridSpawnMilliseconds = 0;
        LastGridReplaceMilliseconds = 0;
        DeferredJobs = 0;
    }
}
