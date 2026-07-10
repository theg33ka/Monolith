using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using System.Numerics;

namespace Content.Server._Forge.ShipRepair.Components;

/// <summary>
/// Ship-mounted repair laser controller. Hits activate timed repair work on a foreign grid.
/// </summary>
[RegisterComponent]
public sealed partial class ShipRepairLaserComponent : Component
{
    [DataField]
    public bool EnableTileRepair = true;

    [DataField]
    public bool EnableEntityRepair = true;

    [DataField]
    public float ActiveDuration = 5f;

    [DataField]
    public float RepairTimeMultiplier = 0.333f;

    [DataField]
    public float TileRepairTime = 0.5f;

    [DataField]
    public int TileRepairCost = 1;

    [DataField]
    public float PowerUsePerSecond = 6000f;

    [DataField]
    public EntProtoId ReceiptPrototype = "Paper";

    [DataField]
    public SoundSpecifier ReceiptPrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public float RadarRepairEffectDuration = 0f;

    [DataField]
    public float RadarRepairEffectMinScale = 1.2f;

    [DataField]
    public float RadarRepairEffectMaxScale = 4f;

    [DataField]
    public Color RadarRepairEffectColorA = Color.FromHex("#46d6a0");

    [DataField]
    public Color RadarRepairEffectColorB = Color.FromHex("#d7fff2");

    [ViewVariables]
    public EntityUid? ActiveGrid;

    [ViewVariables]
    public Vector2 ActiveOrigin;

    [ViewVariables]
    public TimeSpan ActiveUntil;

    [ViewVariables]
    public ShipRepairLaserWork? CurrentWork;

    [ViewVariables]
    public EntityUid? LastTargetGrid;

    [ViewVariables]
    public EntityUid? ActiveRadarEffect;

    [ViewVariables]
    public int SessionMatterSpent;

    [ViewVariables]
    public TimeSpan? LastRepairTime;
}

public sealed class ShipRepairLaserWork
{
    public ShipRepairLaserWorkKey Key;
    public Vector2i Indices;
    public int? RepairId;
    public Vector2 LocalPosition;
    public float Delay;
    public int Cost;
    public TimeSpan FinishAt;
}

public readonly record struct ShipRepairLaserWorkKey(EntityUid Grid, Vector2i Indices, int? RepairId);

[RegisterComponent]
public sealed partial class ShipRepairLaserRadarEffectComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public float MinScale;

    [ViewVariables]
    public float MaxScale;

    [ViewVariables]
    public Color ColorA;

    [ViewVariables]
    public Color ColorB;
}
