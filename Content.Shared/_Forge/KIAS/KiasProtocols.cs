using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared._Forge.KIAS;

public enum KiasTrigger : byte { HullImpact, HullDamage, Collision, AtmosDanger, AnomalyGrowth, Contact, Arrival, WeaponFlash, Proximity, CrewCritical, CrewDead, VesselCritical, PowerDeficit, Manual, Fire }
public enum KiasActionKind : byte { Announce, Record, Lights, Suppression, Relay, Pdc, DevicePort, Mayday }
public enum KiasAlert : byte { Normal, Contact, Battle, Emergency }

[RegisterComponent, NetworkedComponent]
public sealed partial class KiasMaydayComponent : Component;

[RegisterComponent]
public sealed partial class KiasProtocolComponent : Component
{
    [DataField]
    public List<KiasProtocolRecord> Protocols = new();
    [DataField]
    public float CriticalDamageThreshold = 300;
    public KiasAlert Alert;
    public readonly Dictionary<int, TimeSpan> Cooldowns = new();
    public TimeSpan MaydayAfter;
    public TimeSpan DamageWindow;
    public float RecentDamage;
    public uint Revision;
}

[DataDefinition]
public sealed partial class KiasProtocolRecord
{
    [DataField]
    public bool Enabled = true;
    [DataField]
    public KiasTrigger Trigger;
    [DataField]
    public KiasContactDisposition? Disposition;
    [DataField]
    public float MinimumValue;
    [DataField]
    public bool RequireCrewUnavailable;
    [DataField]
    public float Cooldown = 10;
    [DataField]
    public List<KiasProtocolAction> Actions = new();
}

[DataDefinition]
public sealed partial class KiasProtocolAction
{
    [DataField]
    public KiasActionKind Kind;
    [DataField]
    public EntityUid? Target;
    [DataField]
    public string Group = string.Empty;
    [DataField]
    public string Port = string.Empty;
    [DataField]
    public string Message = string.Empty;
    [DataField]
    public bool Value = true;
}

[Serializable, NetSerializable]
public sealed class KiasProtocolMessage : BoundUserInterfaceMessage
{
    public int Index;
    public bool Delete;
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
    public bool Enabled = true;
    public float Cooldown = 10;
}

[Serializable, NetSerializable]
public sealed class KiasControlMessage : BoundUserInterfaceMessage
{
    public bool Reset;
}

[ByRefEvent]
public readonly record struct KiasCrewDistressEvent(EntityUid Grid, EntityUid Person, bool Dead);
[ByRefEvent]
public readonly record struct KiasFireDetectedEvent(EntityUid Grid, EntityUid Source);
