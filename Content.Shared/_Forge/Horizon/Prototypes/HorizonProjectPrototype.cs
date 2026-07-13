using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Forge.Horizon.Prototypes;

[Prototype("horizonProject")]
public sealed partial class HorizonProjectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;

    [DataField(required: true)]
    public HorizonObjectKind Kind { get; private set; }

    [DataField(required: true)]
    public ResPath GridPath { get; private set; }

    [DataField]
    public int RawCost { get; private set; }

    [DataField]
    public int ComponentCost { get; private set; }

    [DataField]
    public int EnergyCost { get; private set; }

    [DataField]
    public int RawIncome { get; private set; }

    [DataField]
    public int EnergyCapacity { get; private set; }

    [DataField]
    public int ProductionCapacity { get; private set; }

    [DataField]
    public int DesiredCount { get; private set; } = 1;

    [DataField]
    public int MaxCount { get; private set; } = 1;

    [DataField]
    public int Priority { get; private set; } = 100;

    [DataField]
    public float MinDistance { get; private set; } = 3000f;

    [DataField]
    public float PreferredDistance { get; private set; } = 5000f;

    [DataField]
    public float MaxDistance { get; private set; } = 8000f;

    [DataField]
    public float ProtectedRadius { get; private set; } = 750f;

    [DataField]
    public bool TemporaryContent { get; private set; } = true;
}
