using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace D3B.App.Controls.WorldMaps;

/// <summary>Draws the route over the world's image as the mockup validated it: a thin teal line on a hairline casing, an arrowhead a leg, and each station a glass pill (D371).</summary>
/// <remarks>
/// <para>
/// In the app's own language (D320): nothing moves, the blue accent marks the chosen step only, the unreachable gap is red.
/// The sizes follow the mockup's, whose map was 64 pixels wide at its zoom 1: <c>k</c> below is a map's width in the view
/// over 64.
/// </para>
/// <para>Hit testing asks <see cref="StationBounds"/> for the pill the drawing used, so that what is drawn is what is pressed.</para>
/// </remarks>
internal static class WorldMapRouteLayer
{
    private const double MockupMapWidth = 64;
    private const double LabelFrom = 0.42;
    private const double LabelSize = 11.5;

    private static readonly Color _teal = Color.Parse("#5FD3C0");
    private static readonly Color _red = Color.Parse("#EA5D52");
    private static readonly Color _accent = Color.Parse("#3B82F6");
    private static readonly IBrush _tealBrush = new SolidColorBrush(_teal);
    private static readonly IBrush _tealTint = new SolidColorBrush(_teal, 0.11);
    private static readonly IBrush _tealGlow = new SolidColorBrush(_teal, 0.16);
    private static readonly IBrush _tealHover = new SolidColorBrush(Color.Parse("#A6EFE2"));
    private static readonly IBrush _redBrush = new SolidColorBrush(_red);
    private static readonly IBrush _casing = new SolidColorBrush(Color.Parse("#09090B"), 0.5);
    private static readonly IBrush _glass = new SolidColorBrush(Color.Parse("#161619"), 0.92);
    private static readonly IBrush _glassSolid = new SolidColorBrush(Color.Parse("#161619"));
    private static readonly IBrush _raised = new SolidColorBrush(Color.Parse("#232328"));
    private static readonly IBrush _numberInk = new SolidColorBrush(Color.Parse("#06241F"));
    private static readonly IBrush _badgeInk = new SolidColorBrush(Color.Parse("#FFB4AD"));
    private static readonly IBrush _label = new SolidColorBrush(Color.Parse("#CFD0D5"));
    private static readonly IBrush _bright = new SolidColorBrush(Color.Parse("#F4F4F6"));
    private static readonly IBrush _arrowHalo = new SolidColorBrush(Color.Parse("#0B1A18"));
    private static readonly IPen _pillEdge = new Pen(new SolidColorBrush(Colors.White, 0.12), 1);
    private static readonly IPen _selectedEdge = new Pen(new SolidColorBrush(_accent), 2);
    private static readonly IPen _ringTeal = new Pen(_tealBrush, 1.5);
    private static readonly IPen _ringRed = new Pen(_redBrush, 1.6);

    /// <summary>Gets the pill of a station in the view.</summary>
    /// <param name="station">The station.</param>
    /// <param name="center">The centre of the station's map in the view.</param>
    /// <param name="mapWidth">A map's width in the view.</param>
    /// <param name="typeface">The label's typeface.</param>
    /// <returns>The pill's bounds; a disc alone when the view is too small for a label.</returns>
    public static Rect StationBounds(WorldMapStation station, Point center, double mapWidth, Typeface typeface)
    {
        var k = mapWidth / MockupMapWidth;
        var radius = Radius(k);

        if (k <= LabelFrom)
            return new Rect(center.X - radius - 3, center.Y - radius - 3, (2 * radius) + 6, (2 * radius) + 6);

        var width = (2 * radius) + 6 + Measure(station.Label, typeface).Width + 14;

        return new Rect(center.X - radius - 3, center.Y - radius - 3, width, (2 * radius) + 6);
    }

    /// <summary>Gets the radius of the disc a station's number is in.</summary>
    /// <param name="mapWidth">A map's width in the view.</param>
    /// <returns>The radius in pixels.</returns>
    public static double DiscRadius(double mapWidth)
    {
        return Radius(mapWidth / MockupMapWidth);
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
        var lineWidth = Math.Max(3, Math.Min(5, mapWidth * 0.07));
        var corner = Math.Max(6, Math.Min(mapWidth, mapHeight) * 0.45);

        for (var i = 0; i < scene.Legs.Count; i++)
            DrawLeg(context, scene.Legs[i], mapWidth, rectOf, lineWidth, corner, typeface);

        if (hover.Kind is WorldMapHitKind.PathPoint)
            DrawPinMark(context, rectOf(hover.X, hover.Y).Center);

        for (var i = 0; i < scene.Stations.Count; i++)
            DrawStation(context, scene.Stations[i], i, mapWidth, rectOf, hover.Kind is WorldMapHitKind.Station && hover.Station == i, typeface);
    }

    private static double Radius(double k)
    {
        return Math.Max(9, (10 * k) + 5.5);
    }

    private static FormattedText Measure(string text, Typeface typeface)
    {
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, LabelSize, _label);
    }

    private static void DrawLeg(DrawingContext context, WorldMapLeg leg, double mapWidth, Func<int, int, Rect> rectOf, double lineWidth, double corner, Typeface typeface)
    {
        if (leg.Squares.Count < 2)
            return;

        var points = leg.Squares.Select(square => rectOf(square.X, square.Y).Center).ToList();

        if (leg.Kind is WorldMapLegKind.Unreachable)
        {
            DrawGap(context, points[0], points[^1], lineWidth, typeface);

            return;
        }

        // The maps the route crosses, barely tinted.
        foreach (var square in leg.Squares.Skip(1).Take(leg.Squares.Count - 2))
            context.DrawRectangle(_tealTint, null, rectOf(square.X, square.Y).Deflate(1));

        var outline = Outline(points, corner);
        var casing = new Pen(_casing, lineWidth + 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var glow = new Pen(_tealGlow, lineWidth * 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var line = new Pen(_tealBrush, lineWidth, leg.IsClosing ? new DashStyle([2.2, 1.6], 0) : null, PenLineCap.Round, PenLineJoin.Round);

        using (context.PushOpacity(leg.IsClosing ? 0.5 : 1))
        {
            context.DrawGeometry(null, glow, outline);
            context.DrawGeometry(null, casing, outline);
            context.DrawGeometry(null, line, outline);

            if (mapWidth > 10)
                DrawArrow(context, points, lineWidth);
        }
    }

    private static StreamGeometry Outline(List<Point> points, double corner)
    {
        var geometry = new StreamGeometry();

        using (var draw = geometry.Open())
        {
            draw.BeginFigure(points[0], false);

            for (var i = 1; i < points.Count - 1; i++)
            {
                var before = Toward(points[i], points[i - 1], corner);
                var after = Toward(points[i], points[i + 1], corner);

                draw.LineTo(before);
                draw.QuadraticBezierTo(points[i], after);
            }

            draw.LineTo(points[^1]);
            draw.EndFigure(false);
        }

        return geometry;
    }

    private static Point Toward(Point from, Point to, double distance)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length < 1e-6)
            return from;

        var step = Math.Min(distance, length / 2) / length;

        return new Point(from.X + (dx * step), from.Y + (dy * step));
    }

    private static void DrawArrow(DrawingContext context, List<Point> points, double lineWidth)
    {
        var total = 0.0;

        for (var i = 0; i + 1 < points.Count; i++)
            total += Distance(points[i], points[i + 1]);

        var half = total / 2;

        for (var i = 0; i + 1 < points.Count; i++)
        {
            var length = Distance(points[i], points[i + 1]);

            if (half > length)
            {
                half -= length;

                continue;
            }

            var along = length > 0 ? half / length : 0;
            var middle = new Point(points[i].X + ((points[i + 1].X - points[i].X) * along), points[i].Y + ((points[i + 1].Y - points[i].Y) * along));
            var angle = Math.Atan2(points[i + 1].Y - points[i].Y, points[i + 1].X - points[i].X);
            var size = (lineWidth * 0.95) + 2;

            using (context.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(middle.X, middle.Y)))
            {
                context.DrawEllipse(_arrowHalo, null, default, size + 3, size + 3);

                var head = new StreamGeometry();

                using (var draw = head.Open())
                {
                    draw.BeginFigure(new Point(size, 0), true);
                    draw.LineTo(new Point(-size * 0.65, -size * 0.8));
                    draw.LineTo(new Point(-size * 0.65, size * 0.8));
                    draw.EndFigure(true);
                }

                context.DrawGeometry(_tealBrush, null, head);
            }

            return;
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

    private static void DrawStation(DrawingContext context, WorldMapStation station, int index, double mapWidth, Func<int, int, Rect> rectOf, bool hovered, Typeface typeface)
    {
        var center = rectOf(station.X, station.Y).Center;
        var k = mapWidth / MockupMapWidth;
        var radius = Radius(k);
        var bounds = StationBounds(station, center, mapWidth, typeface);
        var disc = hovered ? _tealHover : _tealBrush;
        var number = new FormattedText((index + 1).ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(typeface.FontFamily, weight: FontWeight.SemiBold), radius * 0.95, _numberInk);

        if (k > LabelFrom)
        {
            context.DrawRectangle(_glass, station.IsSelected ? _selectedEdge : _pillEdge, bounds, bounds.Height / 2, bounds.Height / 2);
            context.DrawEllipse(disc, null, center, radius, radius);
            context.DrawText(number, new Point(center.X - (number.Width / 2), center.Y - (number.Height / 2)));

            var label = Measure(station.Label, typeface);

            context.DrawText(label, new Point(center.X + radius + 8, center.Y - (label.Height / 2)));
        }
        else
        {
            context.DrawEllipse(_glassSolid, null, center, radius + 3, radius + 3);

            if (station.IsSelected)
                context.DrawEllipse(null, _selectedEdge, center, radius + 4.5, radius + 4.5);

            context.DrawEllipse(disc, null, center, radius, radius);
            context.DrawText(number, new Point(center.X - (number.Width / 2), center.Y - (number.Height / 2)));
        }

        if (station.IsPinned)
            context.DrawEllipse(_bright, null, new Point(center.X + (radius * 0.8), center.Y + (radius * 0.8)), 3, 3);
    }

    private static double Distance(Point a, Point b)
    {
        return Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
    }
}
