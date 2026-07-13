using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Horizon;

[Serializable, NetSerializable]
public enum HorizonDeploymentPhase : byte
{
    Dormant,
    Waking,
    Deploying,
    Operational,
    Degraded,
    Destroyed,
}

[Serializable, NetSerializable]
public enum HorizonObjectKind : byte
{
    Rtr,
    Ams,
    Command,
    Energy,
    Relay,
    Mining,
    Production,
    Defense,
    Amz,
    Technical,
    Salvage,
    Carrier,
}

[Serializable, NetSerializable]
public enum HorizonOrderType : byte
{
    MoveTo,
    DeployStation,
    DefendObject,
    DefendArea,
    ObserveArea,
    SalvageObject,
    DeliverCargo,
    RequestWanderingAi,
    Return,
}

[Serializable, NetSerializable]
public enum HorizonOrderStatus : byte
{
    Queued,
    Active,
    Complete,
    Failed,
    TimedOut,
    Cancelled,
}

[Serializable, NetSerializable]
public enum HorizonAccessTier : byte
{
    Basic,
    Operator,
    Partner,
    Integrated,
}

[Serializable, NetSerializable]
public enum HorizonIffMode : byte
{
    Neutral,
    Restricted,
    Unwanted,
    Hostile,
}
