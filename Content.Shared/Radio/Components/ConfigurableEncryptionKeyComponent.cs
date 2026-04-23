using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Radio.Components;

[RegisterComponent]
public sealed partial class ConfigurableEncryptionKeyComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public int Frequency = 1330;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public int MinFrequency = 1000;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public int MaxFrequency = 3000;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<RadioChannelPrototype>))]
    [ViewVariables(VVAccess.ReadWrite)]
    public string Channel = "Handheld";
}
