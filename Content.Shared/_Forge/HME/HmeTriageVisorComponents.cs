using Content.Shared.Damage.Prototypes;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Forge.HME;

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
