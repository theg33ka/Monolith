using Robust.Shared.GameStates;

namespace Content.Shared._Forge.HME;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class HmeStasisPatientComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Hidden = true;

    [ViewVariables]
    public bool? OriginalVisibility;
}
