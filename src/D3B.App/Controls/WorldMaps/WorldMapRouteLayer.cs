using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace D3B.App.Controls.WorldMaps;

/// <summary>Draws the route over the world's image as the mockup validated it: a thin teal line on a soft shadow, chevrons for its way, the maps it goes through as one outlined shape, and each step a small ringed node on it (D371, D375).</summary>
/// <remarks>
/// <para>
/// In the app's own language (D320): nothing moves, the blue accent marks the chosen step only, the unreachable gap is red.
/// The sizes follow the mockup's, whose map was 64 pixels wide at its zoom 1: <c>k</c> below is a map's width in the view
/// over 64. The owner asked the pills away once the route lay on the real map (D375): a step is a node to press and drag, its
/// number and name are the inspector's.
/// </para>
/// <para>Hit testing asks <see cref="StationBounds"/> for the node the drawing used, a little larger, so that what is drawn is what is pressed.</para>
/// </remarks>
internal static class WorldMapRouteLayer
{
    private const double MockupMapWidth = 64;
    private const double Reach = 5;

    private static readonly Color _teal = Color.Parse("#5FD3C0");
    private static readonly Color _red = Color.Parse("#EA5D52");
    private static readonly Color _accent = Color.Parse("#3B82F6");
    private static readonly IBrush _tealBrush = new SolidColorBrush(_teal);
    private static readonly IBrush _tealTint = new SolidColorBrush(_teal, 0.12);
    private static readonly IBrush _tealEdge = new SolidColorBrush(_teal, 0.92);
    private static readonly IBrush _shade = new SolidColorBrush(Color.Parse("#05070A"), 0.22);
    private static readonly IBrush _tealHover = new SolidColorBrush(Color.Parse("#A6EFE2"));
    private static readonly IBrush _redBrush = new SolidColorBrush(_red);
    private static readonly IBrush _casing = new SolidColorBrush(Color.Parse("#09090B"), 0.5);
    private static readonly IBrush _glassSolid = new SolidColorBrush(Color.Parse("#161619"));
    private static readonly IBrush _raised = new SolidColorBrush(Color.Parse("#232328"));
    private static readonly IBrush _numberInk = new SolidColorBrush(Color.Parse("#06241F"));
    private static readonly IBrush _badgeInk = new SolidColorBrush(Color.Parse("#FFB4AD"));
    private static readonly IBrush _bright = new SolidColorBrush(Color.Parse("#F4F4F6"));
    private static readonly IPen _selectedEdge = new Pen(new SolidColorBrush(_accent), 2);
    private static readonly IPen _ringTeal = new Pen(_tealBrush, 1.5);
    private static readonly IPen _ringRed = new Pen(_redBrush, 1.6);

    /// <summary>Gets the area of a station's node in the view, where it is pressed.</summary>
    /// <param name="center">The centre of the station's map in the view.</param>
    /// <param name="mapWidth">A map's width in the view.</param>
    /// <returns>The area's bounds, a square a little larger than the node.</returns>
    public static Rect StationBounds(Point center, double mapWidth)
    {
        var size = Radius(mapWidth / MockupMapWidth) + Reach;

        return new Rect(center.X - size, center.Y - size, 2 * size, 2 * size);
    }

    /// <summary>Draws the route.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="scene">The route.</param>
    /// <param name="mapWidth">A map's width in the view.</param>
    /// <param name="mapHeight">A map's height in the view.</param>
    /// <param name="rectOf">Gives the view's rectangle of the map at coordinates.</param>
    /// <param name="hover">What the pointer is on.</param>
    /// <param name="typeface">The labels' typeface.</param>
    public static void Draw(DrawingContext context, WorldMapScene scene, double mapWidth, double mapHeight, Func<int, int, Rect> rectOf, WorldMapHit hover, Typeface typeface)
    {
        var lineWidth = Math.Clamp(mapWidth * 0.034, 2, 3.2);

        DrawMaps(context, scene, rectOf, mapWidth);

        for (var i = 0; i < scene.Legs.Count; i++)
            DrawLeg(context, scene.Legs[i], mapWidth, rectOf, lineWidth, typeface);

        if (hover.Kind is WorldMapHitKind.PathPoint)
            DrawPinMark(context, rectOf(hover.X, hover.Y).Center);

        for (var i = 0; i < scene.Stations.Count; i++)
            DrawStation(context, scene.Stations[i], mapWidth, rectOf, hover.Kind is WorldMapHitKind.Station && hover.Station == i);
    }

    // The maps the route goes through are one shape: filled as a union, so that two beside each other do not double their tint, and
    // outlined where no other of them lies beside, so that a shared side is not drawn twice and thicker (D375).
    private static void DrawMaps(DrawingContext context, WorldMapScene scene, Func<int, int, Rect> rectOf, double mapWidth)
    {
        var squares = new HashSet<(int X, int Y)>();

        foreach (var station in scene.Stations)
            squares.Add((station.X, station.Y));

        foreach (var leg in scene.Legs.Where(leg => leg.Kind is not WorldMapLegKind.Unreachable))
        {
            foreach (var square in leg.Squares)
                squares.Add((square.X, square.Y));
        }

        var union = new GeometryGroup { FillRule = FillRule.NonZero };
        var boundary = new StreamGeometry();

        using (var draw = boundary.Open())
        {
            foreach (var (x, y) in squares)
            {
                var rect = rectOf(x, y);

                union.Children.Add(new RectangleGeometry(rect));

                if (!squares.Contains((x, y - 1)))
                    Side(draw, rect.TopLeft, rect.TopRight);

                if (!squares.Contains((x, y + 1)))
                    Side(draw, rect.BottomLeft, rect.BottomRight);

                if (!squares.Contains((x - 1, y)))
                    Side(draw, rect.TopLeft, rect.BottomLeft);

                if (!squares.Contains((x + 1, y)))
                    Side(draw, rect.TopRight, rect.BottomRight);
            }
        }

        var width = Math.Clamp(mapWidth * 0.026, 1.4, 2.2);

        context.DrawGeometry(_tealTint, null, union);
        context.DrawGeometry(null, new Pen(_shade, width + 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), boundary);
        context.DrawGeometry(null, new Pen(_tealEdge, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), boundary);
    }

    private static void Side(StreamGeometryContext draw, Point from, Point to)
    {
        draw.BeginFigure(from, false);
        draw.LineTo(to);
        draw.EndFigure(false);
    }

    private static double Radius(double k)
    {
        return Math.Clamp((1.5 * k) + 2.8, 3.8, 5.8);
    }

    private static void DrawLeg(DrawingContext context, WorldMapLeg leg, double mapWidth, Func<int, int, Rect> rectOf, double lineWidth, Typeface typeface)
    {
        if (leg.Squares.Count < 2)
            return;

        var points = leg.Squares.Select(square => rectOf(square.X, square.Y).Center).ToList();

        if (leg.Kind is WorldMapLegKind.Unreachable)
        {
            DrawGap(context, points[0], points[^1], lineWidth, typeface);

            return;
        }

        var outline = Outline(points, Math.Clamp(mapWidth * 0.2, 4, 16));
        var shadow = new Pen(_shade, lineWidth + 5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var casing = new Pen(_casing, lineWidth + 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var line = new Pen(_tealBrush, lineWidth, leg.IsClosing ? new DashStyle([2.4, 1.8], 0) : null, PenLineCap.Round, PenLineJoin.Round);

        using (context.PushOpacity(leg.IsClosing ? 0.55 : 1))
        {
            context.DrawGeometry(null, shadow, outline);
            context.DrawGeometry(null, casing, outline);
            context.DrawGeometry(null, line, outline);

            if (mapWidth >= 24)
                DrawChevrons(context, points, lineWidth);
        }
    }

    // The route's turns are rounded; the line stays straight between two maps.
    private static StreamGeometry Outline(List<Point> points, double corner)
    {
        var geometry = new StreamGeometry();

        using (var draw = geometry.Open())
        {
            draw.BeginFigure(points[0], false);

            for (var i = 1; i < points.Count - 1; i++)
            {
                draw.LineTo(Toward(points[i], points[i - 1], corner));
                draw.QuadraticBezierTo(points[i], Toward(points[i], points[i + 1], corner));
            }

            draw.LineTo(points[^1]);
            draw.EndFigure(false);
        }

        return geometry;
    }

    private static Point Toward(Point from, Point to, double distance)
    {
        var length = Distance(from, to);

        if (length < 1e-6)
            return from;

        var step = Math.Min(distance, length / 2) / length;

        return new Point(from.X + ((to.X - from.X) * step), from.Y + ((to.Y - from.Y) * step));
    }

    // The way the route goes, a small chevron halfway between each map and the next, the owner's modern touch (D375).
    private static void DrawChevrons(DrawingContext context, List<Point> points, double lineWidth)
    {
        var size = (lineWidth * 1.6) + 2;
        var shadow = new Pen(_casing, (lineWidth * 0.65) + 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var stroke = new Pen(_bright, Math.Max(1.3, lineWidth * 0.65), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        for (var i = 0; i + 1 < points.Count; i++)
        {
            if (Distance(points[i], points[i + 1]) < size * 5)
                continue;

            var middle = new Point((points[i].X + points[i + 1].X) / 2, (points[i].Y + points[i + 1].Y) / 2);
            var angle = Math.Atan2(points[i + 1].Y - points[i].Y, points[i + 1].X - points[i].X);
            var chevron = new StreamGeometry();

            using (var draw = chevron.Open())
            {
                draw.BeginFigure(new Point(-size * 0.45, -size * 0.85), false);
                draw.LineTo(new Point(size * 0.45, 0));
                draw.LineTo(new Point(-size * 0.45, size * 0.85));
                draw.EndFigure(false);
            }

            using (context.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(middle.X, middle.Y)))
            {
                context.DrawGeometry(null, shadow, chevron);
                context.DrawGeometry(null, stroke, chevron);
            }
        }
    }

    private static void DrawGap(DrawingContext context, Point from, Point to, double lineWidth, Typeface typeface)
    {
        var casing = new Pen(_casing, lineWidth + 2.4, lineCap: PenLineCap.Round);
        var dashed = new Pen(_redBrush, lineWidth, new DashStyle([2.2, 1.8], 0), PenLineCap.Round);
        var middle = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);

        context.DrawLine(casing, from, to);
        context.DrawLine(dashed, from, to);
        context.DrawEllipse(_glassSolid, null, middle, 10, 10);
        context.DrawEllipse(null, _ringRed, middle, 9.2, 9.2);

        var mark = new FormattedText("!", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(typeface.FontFamily, weight: FontWeight.SemiBold), 13, _badgeInk);

        context.DrawText(mark, new Point(middle.X - (mark.Width / 2), middle.Y - (mark.Height / 2)));
    }

    private static void DrawPinMark(DrawingContext context, Point center)
    {
        var plus = new Pen(_bright, 1.6);

        context.DrawEllipse(_raised, _ringTeal, center, 10, 10);
        context.DrawLine(plus, new Point(center.X - 3.6, center.Y), new Point(center.X + 3.6, center.Y));
        context.DrawLine(plus, new Point(center.X, center.Y - 3.6), new Point(center.X, center.Y + 3.6));
    }

    private static void DrawStation(DrawingContext context, WorldMapStation station, double mapWidth, Func<int, int, Rect> rectOf, bool hovered)
    {
        var center = rectOf(station.X, station.Y).Center;
        var radius = Radius(mapWidth / MockupMapWidth) * (hovered ? 1.25 : 1);

        context.DrawEllipse(_shade, null, center, radius + 3.6, radius + 3.6);

        if (station.IsSelected)
            context.DrawEllipse(null, _selectedEdge, center, radius + 4.8, radius + 4.8);

        context.DrawEllipse(_bright, null, center, radius + 1.8, radius + 1.8);
        context.DrawEllipse(hovered ? _tealHover : _tealBrush, null, center, radius, radius);

        if (station.IsPinned)
            context.DrawEllipse(_numberInk, null, center, radius * 0.38, radius * 0.38);

        WorldMapBackdropLayer.DrawDots(context, center, radius + 4, station.Actions);
    }

    private static double Distance(Point a, Point b)
    {
        return Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
    }
}
