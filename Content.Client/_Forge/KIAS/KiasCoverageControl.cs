using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Forge.KIAS;

public sealed class KiasCoverageControl : Control
{
    public List<byte> Cells = new();

    public KiasCoverageControl()
    {
        MinSize = new Vector2(270, 270);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = Math.Min(PixelWidth, PixelHeight) / 9f;
        for (var i = 0; i < Math.Min(81, Cells.Count); i++)
        {
            var flag = Cells[i];
            var origin = new Vector2(i % 9 * size, i / 9 * size);
            var rect = new UIBox2(origin + Vector2.One, origin + new Vector2(size - 1));
            handle.DrawRect(rect, (flag & 1) != 0 ? Color.DarkCyan : Color.DarkSlateGray);
            if ((flag & 2) != 0)
                handle.DrawRect(new UIBox2(origin + new Vector2(size * 0.3f), origin + new Vector2(size * 0.7f)), Color.Cyan);
            if ((flag & 4) != 0)
                handle.DrawRect(rect, Color.Yellow, filled: false);
        }
    }
}
