using D3B.App.ViewModels.Routes;
using D3B.Bot.Assets;
using D3B.Bot.Routes;
using D3B.Data.World;
using D3B.Data.WorldMaps;
using D3B.Engine.World;

namespace D3B.App.Tests;

/// <summary>A small world the route editor is tried on: maps 1 to 4 in a line on world 1 at x 1 to 4, y 0, joined both ways; map 5 alone at 5, 0; map 7 alone on world 2 at 1, 1; map 9 an inside at the coordinates of map 2.</summary>
internal static class TestRouteWorld
{
    public const int SubAreaId = 95;
    public const int VaultSubAreaId = 96;

    public static RouteServices Services(AssetLibrary library)
    {
        List<(long From, long To)> scrolls = [(1, 2), (2, 1), (2, 3), (3, 2), (3, 4), (4, 3)];
        long[] maps = [1, 2, 3, 4, 5];
        var vertices = maps.Select(map => new WorldVertex(map, 1)).ToArray();
        var edges = new List<WorldEdge>();
        var transitions = new List<WorldTransition>();

        foreach (var group in scrolls.GroupBy(scroll => (From: (int)scroll.From - 1, To: (int)scroll.To - 1)).OrderBy(group => group.Key))
        {
            edges.Add(new WorldEdge(group.Key.From, group.Key.To, transitions.Count, group.Count()));
            transitions.AddRange(group.Select(scroll => new WorldTransition(WorldTransitionType.Scroll, 0, 0, WorldTransition.None, scroll.To, WorldTransition.None, WorldTransition.None)));
        }

        var atlas = Atlas();
        var graph = new WorldGraph(vertices, edges.ToArray(), transitions.ToArray(), Array.Empty<string>());

        return new RouteServices(library, new RoutePlanner(new WorldPathfinder(graph, atlas)), Index(), Catalog(), atlas, null, (_, _, _) => []);
    }

    /// <summary>Gets world 1, 1,000 by 800 pixels at its one zoom, and world 2, 500 by 400, a map 64 by 46 pixels with the map at 0, 0 at the image's top left.</summary>
    public static WorldMapCatalog Catalog()
    {
        return new WorldMapCatalog(new[] { new WorldMap(1, 0, 0, 0, 64, 46, 0.1f, 2, 0.5f, 1000, 800, true, 0, 1), new WorldMap(2, 0, 0, 0, 64, 46, 0.1f, 2, 0.5f, 500, 400, true, 1, 1) }, new[] { new WorldMapLevel(100, 1000, 800), new WorldMapLevel(100, 500, 400) });
    }

    /// <summary>Gets the maps by coordinates of this world.</summary>
    public static WorldMapIndex Index()
    {
        return new WorldMapIndex(Atlas());
    }

    private static WorldAtlas Atlas()
    {
        long[] maps = [1, 2, 3, 4, 5];
        MapPosition[] positions =
        [
            .. maps.Select(map => new MapPosition(map, (short)map, 0, SubAreaId, 1, 0, 1u << 3)),
            new MapPosition(7, 1, 1, SubAreaId, 2, 0, 0),
            new MapPosition(9, 2, 0, VaultSubAreaId, -1, 0, 0),
        ];
        SubArea[] subAreas = [new(SubAreaId, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0), new(VaultSubAreaId, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0)];

        return new WorldAtlas(positions, subAreas, new[] { new Area(1, 0, 0) }, Array.Empty<Waypoint>(), Array.Empty<int>(), Array.Empty<int>());
    }
}
