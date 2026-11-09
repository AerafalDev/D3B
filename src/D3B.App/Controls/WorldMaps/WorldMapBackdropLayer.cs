using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace D3B.App.Controls.WorldMaps;

/// <summary>Draws what the world holds besides what is edited: the zones tinted, the other routes as faint lines with their steps, the characters with their names (D397).</summary>
/// <remarks>
/// The edited route and zone keep their own layers (<see cref="WorldMapRouteLayer"/>, <see cref="WorldMapZoneLayer"/>); these are
/// quieter, so that what is being worked on stays the brightest thing on the map. Hit testing asks the same geometry the
/// drawing used, so that what is drawn is what is pressed.
/// </remarks>
internal static class WorldMapBackdropLayer
{
    private const double CharacterRadius = 11;
    private const double StepRadius = 3.6;
    private const double CharacterSpacing = 15;

    private static readonly Color _teal = Color.Parse("#5FD3C0");
    private static readonly Color _green = Color.Parse("#A3D47B");
    private static readonly Color _accent = Color.Parse("#3B82F6");
    private static readonly Color _play = Color.Parse("#4ADE80");
    private static readonly IBrush _shade = new SolidColorBrush(Color.Parse("#05070A"), 0.3);
    private static readonly IBrush _bright = new SolidColorBrush(Color.Parse("#F4F4F6"));
    private static readonly IBrush _glass = new SolidColorBrush(Color.Parse("#0E0E10"), 0.82);
    private static readonly IBrush _ink = new SolidColorBrush(Color.Parse("#0B0B0D"));
    private static readonly IPen _accentRing = new Pen(new SolidColorBrush(_accent), 2.2);
    private static readonly IPen _playRing = new Pen(new SolidColorBrush(_play), 2.2);
    private static readonly IPen _plainRing = new Pen(new SolidColorBrush(Colors.White, 0.9), 1.6);

    /// <summary>Gets the colour of a kind of action, the same in the map, the Outliner and the Details.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its colour.</returns>
    public static Color ColorOf(WorldMapActionKind kind)
    {
        return kind switch
        {
            WorldMapActionKind.Harvest => Color.Parse("#7BD389"),
            WorldMapActionKind.Fight => Color.Parse("#EA5D52"),
            WorldMapActionKind.Door => Color.Parse("#E9AD3C"),
            WorldMapActionKind.Npc => Color.Parse("#6EA8FE"),
            _ => Color.Parse("#9AA0AA"),
        };
    }

    /// <summary>Gets where each character is drawn: the centre of its map, shifted sideways when several stand on one.</summary>
    /// <param name="characters">The characters.</param>
    /// <param name="rectOf">Gives the view's rectangle of the map at coordinates.</param>
    /// <returns>A centre for each character, in order.</returns>
    public static IReadOnlyList<Point> CharacterCenters(IReadOnlyList<WorldMapCharacter> characters, Func<int, int, Rect> rectOf)
    {
        var centers = new List<Point>(characters.Count);
        var seen = new Dictionary<(int X, int Y), int>();

        foreach (var character in characters)
        {
            var count = seen.GetValueOrDefault((character.X, character.Y));
            var middle = rectOf(character.X, character.Y).Center;

            seen[(character.X, character.Y)] = count + 1;
            centers.Add(new Point(middle.X + (count * CharacterSpacing), middle.Y));
        }

        return centers;
    }

    /// <summary>Tells whether a point presses a character's disc.</summary>
    /// <param name="center">The disc's centre.</param>
    /// <param name="at">The point.</param>
    /// <returns>Whether it is on the disc.</returns>
    public static bool HitsCharacter(Point center, Point at)
    {
        var dx = center.X - at.X;
        var dy = center.Y - at.Y;

        return (dx * dx) + (dy * dy) <= (CharacterRadius + 3) * (CharacterRadius + 3);
    }

    /// <summary>Tells whether a point presses a step's node.</summary>
    /// <param name="center">The node's centre.</param>
    /// <param name="at">The point.</param>
    /// <returns>Whether it is on the node.</returns>
    public static bool HitsStep(Point center, Point at)
    {
        var dx = center.X - at.X;
        var dy = center.Y - at.Y;

        return (dx * dx) + (dy * dy) <= (StepRadius + 5) * (StepRadius + 5);
    }

    /// <summary>Draws the small dots under a node, one for each kind of thing the step does.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="center">The node's centre.</param>
    /// <param name="radius">The node's radius.</param>
    /// <param name="kinds">What the step does.</param>
    public static void DrawDots(DrawingContext context, Point center, double radius, IReadOnlyList<WorldMapActionKind>? kinds)
    {
        if (kinds is not { Count: > 0 })
            return;

        const double dot = 2.4;
        const double gap = 6.4;

        var left = center.X - ((kinds.Count - 1) * gap / 2);
        var y = center.Y + radius + 7.5;

        for (var i = 0; i < kinds.Count; i++)
        {
            var at = new Point(left + (i * gap), y);

            context.DrawEllipse(_shade, null, at, dot + 1.4, dot + 1.4);
            context.DrawEllipse(new SolidColorBrush(ColorOf(kinds[i])), null, at, dot, dot);
        }
    }

    /// <summary>Draws the zones.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="backdrop">What the world holds.</param>
    /// <param name="mapWidth">A map's width in the view.</param>
    /// <param name="rectOf">Gives the view's rectangle of the map at coordinates.</param>
    public static void DrawZones(DrawingContext context, WorldMapBackdrop backdrop, double mapWidth, Func<int, int, Rect> rectOf)
    {
        foreach (var zone in backdrop.Zones)
        {
            var held = new HashSet<(int X, int Y)>(zone.Squares);

            if (held.Count == 0)
                continue;

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

            var width = Math.Clamp(mapWidth * 0.022, 1.2, 2);

            context.DrawGeometry(new SolidColorBrush(_green, zone.IsSelected ? 0.26 : 0.13), null, union);
            context.DrawGeometry(null, new Pen(_shade, width + 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), boundary);
            context.DrawGeometry(null, new Pen(new SolidColorBrush(_green, zone.IsSelected ? 0.95 : 0.55), width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), boundary);
        }
    }

    /// <summary>Draws the routes: a faint line from step to step, a small node on each, the dots of what it does under it.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="backdrop">What the world holds.</param>
    /// <param name="mapWidth">A map's width in the view.</param>
    /// <param name="rectOf">Gives the view's rectangle of the map at coordinates.</param>
    /// <param name="hit">What the pointer is on.</param>
    /// <param name="typeface">The labels' typeface.</param>
    public static void DrawRoutes(DrawingContext context, WorldMapBackdrop backdrop, double mapWidth, Func<int, int, Rect> rectOf, WorldMapHit hit, Typeface typeface)
    {
        foreach (var route in backdrop.Routes)
        {
            if (route.Steps.Count == 0)
                continue;

            var points = route.Steps.Select(step => rectOf(step.X, step.Y).Center).ToList();
            var alpha = route.IsSelected ? 0.95 : 0.55;
            var width = route.IsSelected ? 2.6 : 1.8;

            if (points.Count > 1)
            {
                var line = new StreamGeometry();

                using (var draw = line.Open())
                {
                    draw.BeginFigure(points[0], false);

                    for (var i = 1; i < points.Count; i++)
                        draw.LineTo(points[i]);

                    if (route.IsLoop && points.Count > 2)
                        draw.LineTo(points[0]);

                    draw.EndFigure(false);
                }

                context.DrawGeometry(null, new Pen(_shade, width + 3, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), line);
                context.DrawGeometry(null, new Pen(new SolidColorBrush(_teal, alpha), width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), line);
            }

            for (var i = 0; i < route.Steps.Count; i++)
            {
                var step = route.Steps[i];
                var hovered = hit.Kind is WorldMapHitKind.RouteStep && hit.Id == route.Id && hit.Step == step.Index;
                var radius = StepRadius * (hovered ? 1.3 : 1);

                context.DrawEllipse(_shade, null, points[i], radius + 2.4, radius + 2.4);
                context.DrawEllipse(new SolidColorBrush(Colors.White, hovered || route.IsSelected ? 1 : 0.75), null, points[i], radius + 1.2, radius + 1.2);
                context.DrawEllipse(new SolidColorBrush(_teal, hovered || route.IsSelected ? 1 : 0.85), null, points[i], radius, radius);
                DrawDots(context, points[i], radius, step.Actions);
            }

            if (mapWidth >= 18)
                DrawLabel(context, route.Name, points[0], -(StepRadius + 9), typeface, _teal);
        }
    }

    /// <summary>Draws the characters.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="backdrop">What the world holds.</param>
    /// <param name="rectOf">Gives the view's rectangle of the map at coordinates.</param>
    /// <param name="hit">What the pointer is on.</param>
    /// <param name="typeface">The labels' typeface.</param>
    public static void DrawCharacters(DrawingContext context, WorldMapBackdrop backdrop, Func<int, int, Rect> rectOf, WorldMapHit hit, Typeface typeface)
    {
        var centers = CharacterCenters(backdrop.Characters, rectOf);

        for (var i = 0; i < backdrop.Characters.Count; i++)
        {
            var character = backdrop.Characters[i];
            var center = centers[i];
            var hovered = hit.Kind is WorldMapHitKind.Character && hit.Id == character.Key;
            var radius = CharacterRadius * (hovered ? 1.1 : 1);
            var ring = character.IsPlaying ? _playRing : character.IsSelected ? _accentRing : _plainRing;

            context.DrawEllipse(_shade, null, center + new Point(0, 1.5), radius + 4, radius + 4);
            context.DrawEllipse(new SolidColorBrush(character.Color), ring, center, radius, radius);

            var initials = new FormattedText(character.Initials, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(typeface.FontFamily, weight: FontWeight.Bold), 9.5, _ink);

            context.DrawText(initials, new Point(center.X - (initials.Width / 2), center.Y - (initials.Height / 2)));
            DrawLabel(context, character.Name, center, -(radius + 11), typeface, null);
        }
    }

    private static void DrawLabel(DrawingContext context, string text, Point center, double dy, Typeface typeface, Color? tint)
    {
        if (text.Length == 0)
            return;

        var label = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, tint is { } color ? new SolidColorBrush(color) : _bright);
        var box = new Rect(center.X - (label.Width / 2) - 6, center.Y + dy - (label.Height / 2) - 2, label.Width + 12, label.Height + 4);

        context.DrawRectangle(_glass, null, box, 6, 6);
        context.DrawText(label, new Point(box.X + 6, box.Y + 2));
    }

    private static void Side(StreamGeometryContext draw, Point from, Point to)
    {
        draw.BeginFigure(from, false);
        draw.LineTo(to);
        draw.EndFigure(false);
    }
}
