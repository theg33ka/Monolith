using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Radio;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.HME;

[RegisterComponent]
public sealed partial class HmeSmartInfusionStandComponent : Component
{
    [DataField]
    public float Range = 1.5f;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(1);

    [DataField]
    public FixedPoint2 MaxReagentDose = FixedPoint2.New(5);

    [DataField]
    public Dictionary<string, List<string>> DamageTypeReagents = new()
    {
        ["Blunt"] = new() { "Bicaridine" },
        ["Slash"] = new() { "Bicaridine" },
        ["Piercing"] = new() { "Bicaridine" },
        ["Heat"] = new() { "Dermaline", "Kelotane" },
        ["Shock"] = new() { "Dermaline", "Kelotane" },
        ["Cold"] = new() { "Dermaline", "Kelotane" },
        ["Caustic"] = new() { "Dermaline", "Kelotane" },
        ["Asphyxiation"] = new() { "DexalinPlus", "Dexalin" },
        ["Bloodloss"] = new() { "Saline" },
        ["Poison"] = new() { "Dylovene" },
        ["Radiation"] = new() { "Arithrazine", "Dylovene" },
        ["Cellular"] = new() { "Arithrazine" },
    };

    [ViewVariables]
    public TimeSpan NextUpdate;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HmeTriageVisorComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<DamageContainerPrototype>> DamageContainers = new()
    {
        "Biological"
    };

    [DataField, AutoNetworkedField]
    public Dictionary<string, ProtoId<HealthIconPrototype>> DamageGroupIcons = new()
    {
        ["Brute"] = "HmeTriageBruteIcon",
        ["Burn"] = "HmeTriageBurnIcon",
        ["Airloss"] = "HmeTriageAirlossIcon",
        ["Toxin"] = "HmeTriageToxinIcon",
        ["Genetic"] = "HmeTriageGeneticIcon",
        ["Metaphysical"] = "HmeTriageMetaphysicalIcon",
    };
}

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
    public Dictionary<string, float> OriginalFixtureDensities = new();
}

[RegisterComponent]
public sealed partial class HmeStasisBlossomCocoonComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(120);

    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public float MetabolicMultiplier = 4f;

    [DataField]
    public DamageSpecifier StabilizingDamage = new()
    {
        DamageDict =
        {
            ["Asphyxiation"] = FixedPoint2.New(-0.25),
            ["Bloodloss"] = FixedPoint2.New(-0.15),
        }
    };

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public TimeSpan NextHealTime;

    [ViewVariables]
    public bool MetabolismApplied;
}

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

[Serializable, NetSerializable]
public sealed partial class HmeStasisBlossomDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class HmeStasisBlossomToggleEvent : InstantActionEvent;

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
public enum HmeInfusionStandVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum HmeInfusionStandVisualState : byte
{
    Empty,
    Idle,
    Filled,
    Active,
}

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
    Spent,
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
