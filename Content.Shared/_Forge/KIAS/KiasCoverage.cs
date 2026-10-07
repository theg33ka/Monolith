using Robust.Shared.Serialization;

namespace Content.Shared._Forge.KIAS;

public enum KiasCoverageShape : byte { Circle, Sector, Data }

[Serializable, NetSerializable]
public sealed class KiasCoverageGeometry
{
    public NetEntity Target;
    public NetEntity Grid;
    public KiasCoverageShape Shape;
    public float Radius;
    public float Arc = 360;
    public bool Large;
    public List<Vector2i> Cells = new();
    public uint Revision;
}
