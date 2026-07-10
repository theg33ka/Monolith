using Content.Shared.FixedPoint;
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
    public FixedPoint2 TransferAmount = FixedPoint2.New(0.2);

    [DataField]
    public FixedPoint2 MaxSourceVolume = FixedPoint2.New(100);

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
