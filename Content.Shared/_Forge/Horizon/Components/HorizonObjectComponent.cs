using Robust.Shared.GameStates;

namespace Content.Shared._Forge.Horizon.Components;

/// <summary>
/// Local identity and aggregate contribution for an АКС Horizon object.
/// Strategic state remains in the server system.
/// </summary>
[RegisterComponent]
public sealed partial class HorizonObjectComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string ObjectId = string.Empty;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public HorizonObjectKind Kind;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ProjectId = string.Empty;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ClusterId = string.Empty;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Dormant;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Active = true;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int RawIncome;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int EnergyCapacity;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int ProductionCapacity;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ProtectedRadius = 750f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int BranchDepth;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool TemporaryContent;
}
