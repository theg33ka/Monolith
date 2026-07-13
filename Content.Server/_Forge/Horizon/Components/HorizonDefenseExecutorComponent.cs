using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._Forge.Horizon.Components;

[RegisterComponent]
public sealed partial class HorizonDefenseExecutorComponent : Component
{
    [ViewVariables]
    public Guid? OrderId;

    [ViewVariables]
    public string IncidentKey = string.Empty;

    [ViewVariables]
    public MapId HomeMap;

    [ViewVariables]
    public Vector2 HomePosition;

    [ViewVariables]
    public TimeSpan ReturnAt;

    [ViewVariables]
    public bool Returning;

    [ViewVariables]
    public bool Busy;
}
