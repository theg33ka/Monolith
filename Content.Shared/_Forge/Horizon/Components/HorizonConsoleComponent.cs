using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Stacks;

namespace Content.Shared._Forge.Horizon.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HorizonConsoleComponent : Component
{
    [DataField]
    public bool AcceptsResources;

    [DataField]
    public List<ProtoId<StackPrototype>> AcceptedStacks = new();
}

[Serializable, NetSerializable]
public enum HorizonConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class HorizonConsoleRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class HorizonConsoleContributeMessage : BoundUserInterfaceMessage
{
    public readonly int RequestedUnits;

    public HorizonConsoleContributeMessage(int requestedUnits)
    {
        RequestedUnits = requestedUnits;
    }
}

[Serializable, NetSerializable]
public sealed class HorizonConsoleHandoffMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class HorizonConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly HorizonDeploymentPhase Phase;
    public readonly string Cluster;
    public readonly string Organization;
    public readonly HorizonAccessTier Access;
    public readonly HorizonIffMode Iff;
    public readonly int Contribution;
    public readonly int Damage;
    public readonly int RawResources;
    public readonly int Components;
    public readonly int Energy;
    public readonly int Objects;
    public readonly int Orders;
    public readonly int Incidents;
    public readonly bool AcceptsResources;
    public readonly string Need;
    public readonly string Diagnostics;
    public readonly bool CanHandoff;
    public readonly bool CarrierControlled;
    public readonly string WanderingAiBrief;

    public HorizonConsoleBoundUserInterfaceState(
        HorizonDeploymentPhase phase,
        string cluster,
        string organization,
        HorizonAccessTier access,
        HorizonIffMode iff,
        int contribution,
        int damage,
        int rawResources,
        int components,
        int energy,
        int objects,
        int orders,
        int incidents,
        bool acceptsResources,
        string need,
        string diagnostics,
        bool canHandoff,
        bool carrierControlled,
        string wanderingAiBrief)
    {
        Phase = phase;
        Cluster = cluster;
        Organization = organization;
        Access = access;
        Iff = iff;
        Contribution = contribution;
        Damage = damage;
        RawResources = rawResources;
        Components = components;
        Energy = energy;
        Objects = objects;
        Orders = orders;
        Incidents = incidents;
        AcceptsResources = acceptsResources;
        Need = need;
        Diagnostics = diagnostics;
        CanHandoff = canHandoff;
        CarrierControlled = carrierControlled;
        WanderingAiBrief = wanderingAiBrief;
    }
}
