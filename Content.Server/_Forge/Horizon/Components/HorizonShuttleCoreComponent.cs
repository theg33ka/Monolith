namespace Content.Server._Forge.Horizon.Components;

/// <summary>
/// Local executor state for a Horizon autonomous shuttle.
/// Movement itself is delegated to the existing HTN shuttle autopilot.
/// </summary>
[RegisterComponent]
public sealed partial class HorizonShuttleCoreComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public Guid? OrderId;

    [ViewVariables(VVAccess.ReadWrite)]
    public string DeployProjectId = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Deploying;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan DeployAt;
}
