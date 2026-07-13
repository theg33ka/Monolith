namespace Content.Server._Forge.Horizon.Components;

[RegisterComponent]
public sealed partial class HorizonWanderingAiComponent : Component
{
    [DataField]
    public string Goal = "Prepare the sector for sustainable autonomous settlement.";

    [DataField]
    public string Context = "You are the single mobile operator of the Horizon network.";

    [DataField]
    public string Permissions = "Operate Horizon consoles and the designated AMU-05 carrier only.";
}

[RegisterComponent]
public sealed partial class HorizonWanderingCarrierComponent : Component
{
}
