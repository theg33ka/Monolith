using Content.Shared._Forge.HME;

namespace Content.Client._Forge.HME;

[RegisterComponent]
public sealed partial class HmeStasisBlossomVisualsComponent : Component
{
    [ViewVariables]
    public HmeStasisBlossomVisualState? LastState;

    [DataField]
    public TimeSpan DeployAnimationDuration = TimeSpan.FromSeconds(1.3);
}
