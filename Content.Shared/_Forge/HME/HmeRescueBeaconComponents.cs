using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.HME;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HmeRescueBeaconComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Armed = true;

    [DataField, AutoNetworkedField]
    public bool Spent;

    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel = "Harmony";

    [DataField]
    public float RescueDistance = 750f;

    [DataField]
    public float RescueJitter = 250f;

    [DataField]
    public float EmpRange = 3f;

    [DataField]
    public float EmpEnergyConsumption = 5000f;

    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public float ExplosionIntensity = 5f;

    [DataField]
    public float ExplosionSlope = 10f;

    [DataField]
    public float MaxTileIntensity = 5f;

    [DataField]
    public DamageSpecifier RescueCriticalDamage = new()
    {
        DamageDict =
        {
            ["Bloodloss"] = FixedPoint2.New(120),
        }
    };
}

[ByRefEvent]
public struct HmeSalvageMobCleanupAttemptEvent
{
    public readonly EntityUid Grid;
    public readonly EntityUid Target;
    public bool Handled;

    public HmeSalvageMobCleanupAttemptEvent(EntityUid grid, EntityUid target)
    {
        Grid = grid;
        Target = target;
        Handled = false;
    }
}

[Serializable, NetSerializable]
public enum HmeRescueBeaconVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum HmeRescueBeaconVisualState : byte
{
    Idle,
    Armed,
    Spent,
}
