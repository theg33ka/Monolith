namespace Content.Server._Forge.Horizon.Components;

[RegisterComponent]
public sealed partial class HorizonWanderingAiComponent : Component
{
    [DataField]
    public string Goal = "horizon-wandering-ai-goal-text";

    [DataField]
    public string Context = "horizon-wandering-ai-context-text";

    [DataField]
    public string Permissions = "horizon-wandering-ai-permissions-text";
}

[RegisterComponent]
public sealed partial class HorizonWanderingCarrierComponent : Component
{
}
