using Robust.Shared.Maths;

namespace Content.Shared._Forge.ShipWeapons.Components;

/// <summary>
/// Restricts fire-control shots to a fixed forward arc instead of allowing gimbal aiming.
/// </summary>
[RegisterComponent]
public sealed partial class FixedMountFireArcComponent : Component
{
    /// <summary>
    /// Total allowed firing arc around the weapon's forward direction.
    /// </summary>
    [DataField]
    public Angle Arc = Angle.FromDegrees(12);

    /// <summary>
    /// Optional correction for sprites whose drawn barrel does not match transform forward.
    /// </summary>
    [DataField]
    public Angle ForwardOffset = Angle.Zero;
}
