namespace Content.Server._Forge.ShipRepair.Components;

/// <summary>
/// Cumulative repair matter spent by ship repair lasers on this grid.
/// </summary>
[RegisterComponent]
public sealed partial class ShipRepairLaserLedgerComponent : Component
{
    [ViewVariables]
    public int MatterSpent;

    [ViewVariables]
    public TimeSpan? LastRepairTime;
}
