using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace D3B.App.Controls.WorldMaps;

/// <summary>Draws the client's own pictures of the points of interest over the world's image, and finds the one under the pointer (D414).</summary>
/// <remarks>
/// <para>
/// 679 hints stand on the main world, 4 at most on one map, so what is drawn is chosen at each zoom: a hint shows from a width
/// of map that grows as it is less useful (<see cref="AppearsFrom"/>), and of the pictures that would overlap, the most useful
/// is kept. The choice reads every hint of the world, never only those in view, so that it does not change when the image is moved.
/// </para>
/// <para>Drawing and hit testing ask the same layout, so that what is drawn is what is pressed.</para>
/// </remarks>
internal static class WorldMapHintLayer
{
    private const double MinSize = 18;
    private const double MaxSize = 30;
    private const double Hovered = 1.16;
    private const double Spread = 1.12;

    private static readonly IBrush _glass = new SolidColorBrush(Color.Parse("#0E0E10"), 0.9);
    private static readonly IBrush _bright = new SolidColorBrush(Color.Parse("#F4F4F6"));
    private static readonly IBrush _halo = new SolidColorBrush(Colors.White, 0.22);
    private static readonly IBrush _mark = new SolidColorBrush(Color.Parse("#E9AD3C"));
    private static readonly IPen _markRing = new Pen(new SolidColorBrush(Color.Parse("#0B0B0D")), 1.6);

    /// <summary>Gives the width and height of a hint's picture, which grows with the map and stays within what reads at a glance.</summary>
    /// <param name="mapWidth">The width of a map, in pixels.</param>
    /// <returns>The picture's side, in pixels.</returns>
    public static double IconSize(double mapWidth)
    {
        return Math.Clamp(mapWidth * 0.6, MinSize, MaxSize);
    }

    /// <summary>Gives the width of a map from which a hint of a priority shows.</summary>
    /// <param name="priority">The hint's priority.</param>
    /// <returns>The width, in pixels.</returns>
    public static double AppearsFrom(int priority)
    {
        return priority switch
        {
            >= 70 => 4,
            >= 50 => 8,
            >= 30 => 14,
            _ => 26,
        };
    }

    /// <summary>Chooses the hints to draw at this zoom and where, the most useful first kept.</summary>
    /// <param name="hints">Every hint of the world shown.</param>
    /// <param name="rectOf">Gives the view's rectangle of the map at coordinates.</param>
    /// <param name="mapWidth">The width of a map, in pixels.</param>
    /// <returns>The hints kept with their squares, the most useful last, as they are drawn.</returns>
    public static IReadOnlyList<WorldMapPlacedHint> Layout(IReadOnlyList<WorldMapHint> hints, Func<int, int, Rect> rectOf, double mapWidth)
    {
        var size = IconSize(mapWidth);
        var shown = hints
            .Where(hint => mapWidth >= AppearsFrom(hint.Priority))
            .OrderByDescending(hint => hint.Priority)
            .ThenBy(hint => hint.Id)
            .ToList();
        var square = shown.GroupBy(hint => (hint.X, hint.Y)).ToDictionary(group => group.Key, group => group.ToList());
        var placed = new List<WorldMapPlacedHint>();
        var taken = new Dictionary<(int Column, int Row), List<Rect>>();

        foreach (var hint in shown)
        {
            var siblings = square[(hint.X, hint.Y)];
            var center = rectOf(hint.X, hint.Y).Center;

            // Hints on one square lie side by side when it is wide enough, and else the most useful alone keeps it.
            if (siblings.Count > 1 && mapWidth >= size * Spread * siblings.Count)
                center += new Point((siblings.IndexOf(hint) - ((siblings.Count - 1) / 2.0)) * size * Spread, 0);

            var bounds = new Rect(center.X - (size / 2), center.Y - (size / 2), size, size);

            if (Overlaps(taken, bounds, size))
                continue;

            var cell = CellOf(center, size);

            if (!taken.TryGetValue(cell, out var rects))
                taken[cell] = rects = [];

            rects.Add(bounds);
            placed.Add(new WorldMapPlacedHint(hint, bounds));
        }

        placed.Reverse();

        return placed;
    }

    /// <summary>Finds the hint whose picture lies under a point.</summary>
    /// <param name="placed">What <see cref="Layout"/> kept.</param>
    /// <param name="at">The point, in the view's own.</param>
    /// <returns>The hint drawn on top there, or <see langword="null"/>.</returns>
    public static WorldMapHint? HitAt(IReadOnlyList<WorldMapPlacedHint> placed, Point at)
    {
        for (var i = placed.Count - 1; i >= 0; i--)
        {
            if (placed[i].Bounds.Contains(at))
                return placed[i].Hint;
        }

        return null;
    }

    /// <summary>Gives the box of the name written over the hint under the pointer: above its picture, kept inside the view.</summary>
    /// <param name="bounds">The picture's square.</param>
    /// <param name="text">The size of the name's text.</param>
    /// <param name="view">The view's own rectangle.</param>
    /// <returns>The box, which falls under the picture when there is no room above it.</returns>
    public static Rect LabelBox(Rect bounds, Size text, Rect view)
    {
        var width = text.Width + 14;
        var height = text.Height + 6;
        var x = Math.Clamp(bounds.Center.X - (width / 2), view.Left + 4, Math.Max(view.Left + 4, view.Right - width - 4));
        var above = bounds.Top - height - 5;
        var y = above >= view.Top + 4 ? above : bounds.Bottom + 5;

        return new Rect(x, y, width, height);
    }

    /// <summary>Draws the hints kept, and the name of the one under the pointer.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="placed">What <see cref="Layout"/> kept.</param>
    /// <param name="hovered">The identifier of the hint under the pointer, or <see langword="null"/>.</param>
    /// <param name="typeface">The label's typeface.</param>
    /// <param name="view">The view's own rectangle.</param>
    public static void Draw(DrawingContext context, IReadOnlyList<WorldMapPlacedHint> placed, int? hovered, Typeface typeface, Rect view)
    {
        foreach (var (hint, bounds) in placed)
        {
            var lifted = hovered == hint.Id;
            var drawn = lifted ? bounds.Inflate(bounds.Width * (Hovered - 1) / 2) : bounds;

            if (lifted)
                context.DrawEllipse(_halo, null, drawn.Center, drawn.Width * 0.62, drawn.Width * 0.62);

            if (hint.Icon?.Invoke() is { } icon)
                context.DrawImage(icon, new Rect(icon.Size), drawn);
            else
                context.DrawEllipse(_mark, _markRing, drawn.Center, drawn.Width * 0.22, drawn.Width * 0.22);
        }

        foreach (var (hint, bounds) in placed)
        {
            if (hovered != hint.Id)
                continue;

            var label = new FormattedText(hint.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12, _bright);
            var box = LabelBox(bounds, new Size(label.Width, label.Height), view);

            context.DrawRectangle(_glass, null, box, 6, 6);
            context.DrawText(label, new Point(box.X + 7, box.Y + 3));
        }
    }

    private static (int Column, int Row) CellOf(Point point, double size)
    {
        return ((int)Math.Floor(point.X / size), (int)Math.Floor(point.Y / size));
    }

    private static bool Overlaps(Dictionary<(int Column, int Row), List<Rect>> taken, Rect bounds, double size)
    {
        var (column, row) = CellOf(bounds.Center, size);

        for (var dc = -1; dc <= 1; dc++)
        {
            for (var dr = -1; dr <= 1; dr++)
            {
                if (taken.TryGetValue((column + dc, row + dr), out var rects) && rects.Any(rect => rect.Intersects(bounds)))
                    return true;
            }
        }

        return false;
    }
}
