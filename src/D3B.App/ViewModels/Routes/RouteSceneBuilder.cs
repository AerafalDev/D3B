using D3B.App.Controls.WorldMaps;
using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>Draws a route on the world's map: a station for each anchor on the layer shown, and the path of each leg between two (D299, D371).</summary>
internal static class RouteSceneBuilder
{
    /// <summary>Makes the scene of a route for one layer.</summary>
    /// <param name="services">What the editors read: the world's index.</param>
    /// <param name="texts">What says a map's sub-area for its station.</param>
    /// <param name="anchors">The route's anchors.</param>
    /// <param name="legs">The legs the planner found, as far as it has.</param>
    /// <param name="layer">The world shown.</param>
    /// <param name="selected">The step chosen, or -1.</param>
    /// <returns>The scene, with the step of each station and the leg of each path.</returns>
    public static RouteSceneParts Build(RouteServices services, RouteTexts texts, IReadOnlyList<RouteAnchor> anchors, IReadOnlyList<RouteLeg> legs, int layer, int selected)
    {
        var index = services.Index;
        var stations = new List<WorldMapStation>();
        var paths = new List<WorldMapLeg>();
        var stationAnchors = new List<int>();
        var sceneLegs = new List<int>();

        for (var i = 0; i < anchors.Count; i++)
        {
            var anchor = anchors[i];

            if (!index.TryPlace(anchor.MapId, out var anchorLayer, out var x, out var y) || anchorLayer != layer)
                continue;

            stations.Add(new WorldMapStation(x, y, texts.Zone(anchor.MapId), anchor.IsPinned, i == selected, RouteActionKinds.Of(anchor.Actions)));
            stationAnchors.Add(i);
        }

        for (var j = 0; j < legs.Count && j < anchors.Count; j++)
        {
            var leg = legs[j];

            if (leg.Kind == RouteLegKind.Same || !index.TryPlace(leg.From, out var fromLayer, out var fx, out var fy) || !index.TryPlace(leg.To, out var toLayer, out var tx, out var ty) || fromLayer != layer || toLayer != layer)
                continue;

            List<(int X, int Y)> squares = [(fx, fy)];

            foreach (var between in leg.Between)
            {
                if (index.TryPlace(between, out var betweenLayer, out var bx, out var by) && betweenLayer == layer && !EndsAt(squares, bx, by))
                    squares.Add((bx, by));
            }

            if (!EndsAt(squares, tx, ty))
                squares.Add((tx, ty));

            if (squares.Count < 2)
                continue;

            paths.Add(new WorldMapLeg(squares, leg.Kind == RouteLegKind.Unreachable ? WorldMapLegKind.Unreachable : WorldMapLegKind.Path, j == anchors.Count - 1));
            sceneLegs.Add(j);
        }

        return new RouteSceneParts(new WorldMapScene(stations, paths), stationAnchors, sceneLegs);
    }

    private static bool EndsAt(List<(int X, int Y)> squares, int x, int y)
    {
        return squares.Count > 0 && squares[^1] == (x, y);
    }
}
