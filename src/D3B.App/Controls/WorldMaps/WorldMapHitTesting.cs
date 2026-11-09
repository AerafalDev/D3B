using System.Globalization;
using Avalonia;

namespace D3B.App.Controls.WorldMaps;

/// <summary>Finds what lies under the pointer on the world's map: a station, a character, a step, a point of a path, a zone, a shared mark or a plain square (D308, D371).</summary>
internal static class WorldMapHitTesting
{
    /// <summary>The width of a map, in pixels, from which the grid shows.</summary>
    public const double GridFrom = 14;

    /// <summary>The width of a map, in pixels, from which the mark of shared coordinates is a badge with a count.</summary>
    public const double BadgeFrom = 96;

    /// <summary>Gives the circle of the mark of coordinates that several maps share.</summary>
    /// <param name="rect">The rectangle of the map's square.</param>
    /// <param name="mapWidth">The width of a map, in pixels.</param>
    /// <returns>The circle's centre and radius.</returns>
    public static (Point Center, double Radius) SharedMark(Rect rect, double mapWidth)
    {
        var k = mapWidth / 64;
        var radius = mapWidth < BadgeFrom ? Math.Clamp(mapWidth / 16, 2.5, 4.5) : Math.Max(6, (8 * k) + 2.5);

        return (new Point(rect.Right - 3 - radius, rect.Top + 3 + radius), radius);
    }

    /// <summary>Gives the square of the world under a point, if a map lies there.</summary>
    /// <param name="at">The point, in the view's own.</param>
    /// <param name="c">What the view gives.</param>
    /// <returns>The square's coordinates, or <see langword="null"/>.</returns>
    public static (int X, int Y)? SquareAt(Point at, in WorldMapHitContext c)
    {
        var (ix, iy) = c.Viewport.ToImage(at.X, at.Y);
        var x = (int)Math.Floor((ix - c.World.OriginX) / c.World.MapWidth);
        var y = (int)Math.Floor((iy - c.World.OriginY) / c.World.MapHeight);

        return c.Source.Index.MapsAt(c.Layer, x, y).Length > 0 ? (x, y) : null;
    }

    /// <summary>Gives what lies under a point.</summary>
    /// <param name="at">The point, in the view's own.</param>
    /// <param name="c">What the view gives.</param>
    /// <returns>What it is.</returns>
    public static WorldMapHit HitAt(Point at, in WorldMapHitContext c)
    {
        var scene = c.Scene;
        var mapWidth = c.World.MapWidth * c.Viewport.Scale;

        for (var i = scene.Stations.Count - 1; i >= 0; i--)
        {
            var station = scene.Stations[i];

            if (WorldMapRouteLayer.StationBounds(c.RectOf(station.X, station.Y).Center, mapWidth).Contains(at))
                return new WorldMapHit(WorldMapHitKind.Station, station.X, station.Y, i, -1);
        }

        if (c.Tool is WorldMapTool.Select && scene.Backdrop is { } backdrop)
        {
            var centers = WorldMapBackdropLayer.CharacterCenters(backdrop.Characters, c.RectOf);

            for (var i = backdrop.Characters.Count - 1; i >= 0; i--)
            {
                if (WorldMapBackdropLayer.HitsCharacter(centers[i], at))
                    return new WorldMapHit(WorldMapHitKind.Character, backdrop.Characters[i].X, backdrop.Characters[i].Y, -1, -1, backdrop.Characters[i].Key);
            }

            foreach (var route in backdrop.Routes)
            {
                foreach (var step in route.Steps)
                {
                    if (WorldMapBackdropLayer.HitsStep(c.RectOf(step.X, step.Y).Center, at))
                        return new WorldMapHit(WorldMapHitKind.RouteStep, step.X, step.Y, -1, -1, route.Id, step.Index);
                }
            }
        }

        var reach = Math.Max(8, Math.Min(mapWidth, c.World.MapHeight * c.Viewport.Scale) / 3);

        for (var leg = 0; leg < scene.Legs.Count; leg++)
        {
            var path = scene.Legs[leg];

            if (path.Kind is not WorldMapLegKind.Path)
                continue;

            foreach (var square in path.Squares.Skip(1).Take(path.Squares.Count - 2))
            {
                var center = c.RectOf(square.X, square.Y).Center;

                if (Math.Abs(center.X - at.X) <= reach && Math.Abs(center.Y - at.Y) <= reach && Math.Sqrt(((center.X - at.X) * (center.X - at.X)) + ((center.Y - at.Y) * (center.Y - at.Y))) <= reach)
                    return new WorldMapHit(WorldMapHitKind.PathPoint, square.X, square.Y, -1, leg);
            }
        }

        // Under the route and what it holds, over the map's own squares: the pictures lie there, as they are drawn.
        if (c.Hints is { Count: > 0 } hints && WorldMapHintLayer.HitAt(WorldMapHintLayer.Layout(hints, c.RectOf, mapWidth), at) is { } hint)
            return new WorldMapHit(WorldMapHitKind.Hint, hint.X, hint.Y, -1, -1, hint.Id.ToString(CultureInfo.InvariantCulture));

        if (SquareAt(at, c) is not { } over)
            return WorldMapHit.None;

        if (c.Tool is WorldMapTool.Select && scene.Backdrop is { Zones.Count: > 0 } zones)
        {
            // The zone listed last lies over the others, as it is drawn.
            for (var i = zones.Zones.Count - 1; i >= 0; i--)
            {
                if (zones.Zones[i].Holds(over.X, over.Y))
                    return new WorldMapHit(WorldMapHitKind.Zone, over.X, over.Y, -1, -1, zones.Zones[i].Id);
            }
        }

        if (c.ShowShared && mapWidth >= GridFrom && c.Source.Index.MapsAt(c.Layer, over.X, over.Y).Length > 1)
        {
            var (center, radius) = SharedMark(c.RectOf(over.X, over.Y), mapWidth);

            if (Math.Sqrt(((center.X - at.X) * (center.X - at.X)) + ((center.Y - at.Y) * (center.Y - at.Y))) <= radius + (mapWidth < BadgeFrom ? 4 : 2))
                return new WorldMapHit(WorldMapHitKind.Shared, over.X, over.Y, -1, -1);
        }

        return new WorldMapHit(WorldMapHitKind.Square, over.X, over.Y, -1, -1);
    }
}
