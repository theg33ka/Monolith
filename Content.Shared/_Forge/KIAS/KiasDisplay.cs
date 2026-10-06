using Robust.Shared.Serialization;

namespace Content.Shared._Forge.KIAS;

[RegisterComponent]
public sealed partial class KiasDisplayComponent : Component;

[RegisterComponent]
public sealed partial class KiasSpeakerComponent : Component
{
    [DataField]
    public string Group = "SHIP";
    public EntityUid? Tone;
    [DataField]
    public string Message = string.Empty;

    [DataField]
    public List<KiasSpeakerLink> Links = new();
}

[DataDefinition]
public sealed partial class KiasSpeakerLink
{
    [DataField]
    public EntityUid Source;
    [DataField]
    public string SourcePort = string.Empty;
    [DataField]
    public string Message = string.Empty;
}

[RegisterComponent]
public sealed partial class KiasServiceToolComponent : Component
{
    [DataField]
    public KiasServiceMode Mode;
    [DataField]
    public string Message = string.Empty;
    public EntityUid? Source;
    public TimeSpan NextTest;
    public List<byte> Coverage = new();
}

public enum KiasServiceMode : byte { Link, Diagnose, Coverage, Test, Group, Room }

[Serializable, NetSerializable]
public enum KiasUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class KiasUiState : BoundUserInterfaceState
{
    public bool Online;
    public int Entities;
    public int Crew;
    public string Devices = string.Empty;
    public string Log = string.Empty;
    public bool ServiceTool;
    public string Message = string.Empty;
    public string Atmos = string.Empty;
    public List<byte> Coverage = new();
    public string CrewDetails = string.Empty;
    public string Power = string.Empty;
    public string Defence = string.Empty;
    public string Navigation = string.Empty;
    public string Faults = string.Empty;
    public string Alert = string.Empty;
    public bool ProtocolsAvailable;
    public uint ProtocolRevision;
    public List<KiasProtocolView> Protocols = new();
    public List<KiasUiTarget> Targets = new();
}

[Serializable, NetSerializable]
public sealed class KiasUiTarget
{
    public NetEntity Entity;
    public string Name = string.Empty;
}

[Serializable, NetSerializable]
public sealed class KiasProtocolView
{
    public KiasTrigger Trigger;
    public KiasContactDisposition? Disposition;
    public float MinimumValue;
    public bool RequireCrewUnavailable;
    public KiasActionKind Action;
    public NetEntity? Target;
    public string Group = string.Empty;
    public string Port = string.Empty;
    public string Message = string.Empty;
    public bool Value;
    public bool Enabled;
    public float Cooldown;
}

[Serializable, NetSerializable]
public sealed class KiasRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class KiasSetMessage : BoundUserInterfaceMessage
{
    public string Message;
    public KiasSetMessage(string message) => Message = message;
}
