using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.HME;

[RegisterComponent]
public sealed partial class HmeStasisBlossomComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "ActionHMEStasisBlossomToggle";

    [DataField]
    public EntityUid? ToggleActionEntity;

    [DataField]
    public TimeSpan AttachDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan PowerUseInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public float PowerUse = 1f;

    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public float MetabolicMultiplier = 4f;

    [DataField]
    public float TransportDensityScale = 0.02f;

    [DataField]
    public float MinTransportFixtureDensity = 0.1f;

    [DataField]
    public DamageSpecifier StabilizingDamage = new()
    {
        DamageDict =
        {
            ["Asphyxiation"] = FixedPoint2.New(-0.25),
            ["Bloodloss"] = FixedPoint2.New(-0.15),
        }
    };

    [DataField]
    public EntityUid? AttachedTarget;

    [DataField]
    public bool Deployed;

    [ViewVariables]
    public bool MetabolismApplied;

    [ViewVariables]
    public TimeSpan NextHealTime;

    [ViewVariables]
    public TimeSpan NextPowerUse;

    [ViewVariables]
    public EntityUid? TransportAdjustedTarget;

    [ViewVariables]
    public Dictionary<string, HmeFixtureDensityChange> TransportFixtureDensityChanges = new();
}

public readonly record struct HmeFixtureDensityChange(float Original, float Applied);

[Serializable, NetSerializable]
public sealed partial class HmeStasisBlossomDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class HmeStasisBlossomToggleEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum HmeStasisBlossomVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum HmeStasisBlossomVisualState : byte
{
    Compact,
    Attached,
    Deployed,
}
