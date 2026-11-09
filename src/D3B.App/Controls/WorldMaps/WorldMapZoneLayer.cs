using Avalonia;
using Avalonia.Media;

namespace D3B.App.Controls.WorldMaps;

/// <summary>Draws a zone over the world's image: the maps it holds as one tinted shape outlined where no other lies beside, those it leaves out dotted red (D394).</summary>
/// <remarks>The green is the zones' own in the assets (D326), as the route's teal is the routes'.</remarks>
internal static class WorldMapZoneLayer
{
    private static readonly Color _green = Color.Parse("#A3D47B");
    private static readonly Color _red = Color.Parse("#EA5D52");
    private static readonly IBrush _tint = new SolidColorBrush(_green, 0.22);
    private static readonly IBrush _edge = new SolidColorBrush(_green, 0.95);
    private static readonly IBrush _shade = new SolidColorBrush(Color.Parse("#05070A"), 0.22);
    private static readonly IBrush _leftOutTint = new SolidColorBrush(_red, 0.2);
    private static readonly IPen _leftOutPen = new Pen(new SolidColorBrush(_red, 0.9), 1.2, new DashStyle([2, 2], 0));

    /// <summary>Draws the zone of a scene, if it has one.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="scene">The scene.</param>
    /// <param name="mapWidth">A map's width in the view.</param>
    /// <param name="rectOf">Gives the view's rectangle of the map at coordinates.</param>
    public static void Draw(DrawingContext context, WorldMapScene scene, double mapWidth, Func<int, int, Rect> rectOf)
    {
        if (scene.Squares is not { Count: > 0 } squares)
            return;

        var held = new HashSet<(int X, int Y)>();

        foreach (var square in squares)
        {
            if (square.IsLeftOut)
            {
                var rect = rectOf(square.X, square.Y).Deflate(1);

                context.DrawRectangle(_leftOutTint, _leftOutPen, rect);
            }
            else
            {
                held.Add((square.X, square.Y));
            }
        }

        var union = new GeometryGroup { FillRule = FillRule.NonZero };
        var boundary = new StreamGeometry();

        using (var draw = boundary.Open())
        {
            foreach (var (x, y) in held)
            {
                var rect = rectOf(x, y);

                union.Children.Add(new RectangleGeometry(rect));

                if (!held.Contains((x, y - 1)))
                    Side(draw, rect.TopLeft, rect.TopRight);

                if (!held.Contains((x, y + 1)))
                    Side(draw, rect.BottomLeft, rect.BottomRight);

                if (!held.Contains((x - 1, y)))
                    Side(draw, rect.TopLeft, rect.BottomLeft);

                if (!held.Contains((x + 1, y)))
                    Side(draw, rect.TopRight, rect.BottomRight);
            }
        }

        var width = Math.Clamp(mapWidth * 0.026, 1.4, 2.2);

        context.DrawGeometry(_tint, null, union);
        context.DrawGeometry(null, new Pen(_shade, width + 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), boundary);
        context.DrawGeometry(null, new Pen(_edge, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), boundary);
    }

    private static void Side(StreamGeometryContext draw, Point from, Point to)
    {
        draw.BeginFigure(from, false);
        draw.LineTo(to);
        draw.EndFigure(false);
    }
}
