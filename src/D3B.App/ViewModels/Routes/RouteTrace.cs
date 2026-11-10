using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>Decides which map a press of the trace tool, or a drag across the squares, adds to a route (D299).</summary>
internal sealed class RouteTrace
{
    private long _paintedFrom;

    /// <summary>Gets the map a press on the squares at some coordinates adds, and remembers it as the one a drag goes on from.</summary>
    /// <param name="services">What the editors read: the maps by coordinates and the world's path.</param>
    /// <param name="layer">The world shown.</param>
    /// <param name="x">The coordinates.</param>
    /// <param name="y">The coordinates.</param>
    /// <param name="isDrag">Whether a drag crossed the coordinates: it adds a map only when it is a neighbour of the map crossed before by a real exit, else the path fills the gap.</param>
    /// <param name="anchors">The route's anchors.</param>
    /// <returns>The map to add, or 0 when none is.</returns>
    public long Press(RouteServices services, int layer, int x, int y, bool isDrag, IReadOnlyList<RouteAnchor> anchors)
    {
        var maps = services.Index.MapsAt(layer, x, y);

        if (maps.IsEmpty)
            return 0;

        var map = services.Planner.Primary(maps);
        var from = _paintedFrom;

        // A drag goes on from the map it crossed last, whether or not that one became an anchor: a square no exit joins is left to
        // the path, and the drag does not stop there.
        _paintedFrom = map;

        if (anchors.Any(anchor => anchor.MapId == map))
            return 0;

        if (isDrag && from != 0)
        {
            var leg = services.Planner.Find(from, map);

            if (leg.Kind != RouteLegKind.Path || !leg.Between.IsEmpty)
                return 0;
        }

        return map;
    }
}
