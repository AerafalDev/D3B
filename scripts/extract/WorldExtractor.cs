using AssetsTools.NET.Extra;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the world: the client's world graph, and the tables of the atlas (positions, sub-areas, areas, zaaps).</summary>
/// <remarks>
/// <see cref="Check"/> then holds the graph and the atlas against the maps, so that a client changing what the fields
/// mean, such as which bits of a cell's <c>linkedZone</c> name its zone, fails the extraction (client.md).
/// </remarks>
internal static class WorldExtractor
{
    private static readonly string[] _tables = ["mapsinformation", "subareas", "areas", "waypoints"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [GraphPath(client), .. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    private static string GraphPath(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "aa", "StandaloneWindows64", "worldassets_assets_all.bundle");
    }

    public static WorldGraph ReadGraph(ClientInstall client)
    {
        var path = GraphPath(client);

        if (!File.Exists(path))
            throw new FileNotFoundException($"The client has no world graph: {path} does not exist.", path);

        using var bundle = new UnityBundle(path);

        foreach (var file in bundle.Files)
        {
            var metadata = file.file.Metadata;

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.MonoBehaviour))
            {
                var tree = UnityTypeTree.From(metadata.TypeTreeTypes[info.TypeIdOrIndex], "PathFindingData");

                if (!WorldGraphReader.IsGraph(tree))
                    continue;

                try
                {
                    var graph = WorldGraphReader.Read(UnityBundle.ReadObject(file, info), tree);

                    Console.WriteLine($"World graph: {graph.Vertices.Length:N0} vertices, {graph.Edges.Length:N0} edges, {graph.Transitions.Length:N0} transitions, {graph.Criteria.Length:N0} criteria");

                    return graph;
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidDataException($"The world graph does not fit what the extractor measured: {exception.Message}", exception);
                }
            }
        }

        throw new InvalidDataException($"{path} holds no world graph.");
    }

    public static WorldAtlas ReadAtlas(ClientInstall client)
    {
        try
        {
            var positions = UnityDataTable.Read(client, "mapsinformation")
                .Select(row => (row.Key, Position: new MapPosition(
                    row.Record.UInt32("id"),
                    row.Record.Int16("posX"),
                    row.Record.Int16("posY"),
                    row.Record.UInt16("subAreaId"),
                    row.Record.SByte("worldMap"),
                    row.Record.Int32("nameId"),
                    row.Record.UInt32("m_flags"))))
                .ToList();

            foreach (var (key, position) in positions)
            {
                if (key != position.MapId)
                    throw new InvalidDataException($"The maps' information keys map {position.MapId} as {key}.");
            }

            var subAreas = UnityDataTable.Read(client, "subareas").Select(row => new SubArea(
                row.Record.Int32("id"),
                row.Record.Int32("nameId"),
                row.Record.Int32("areaId"),
                checked((int)row.Record.UInt32("level")),
                ZaapOf(row.Record.Int32("id"), row.Record.Int32("associatedZaapMapId")),
                row.Record.Boolean("basicAccountAllowed")));
            var areas = UnityDataTable.Read(client, "areas").Select(row => new Area(
                row.Record.Int32("id"),
                row.Record.Int32("nameId"),
                row.Record.Int32("superAreaId")));
            var waypoints = UnityDataTable.Read(client, "waypoints").Select(row => new Waypoint(
                row.Record.Int32("id"),
                row.Record.Int32("mapId"),
                row.Record.Int32("subAreaId"),
                row.Record.Boolean("activated")));
            var atlas = new WorldAtlas(
                positions.Select(row => row.Position).OrderBy(position => position.MapId).ToArray(),
                subAreas.OrderBy(subArea => subArea.Id).ToArray(),
                areas.OrderBy(area => area.Id).ToArray(),
                waypoints.OrderBy(waypoint => waypoint.Id).ToArray());

            Console.WriteLine($"Atlas: {atlas.Positions.Length:N0} map positions, {atlas.SubAreas.Length:N0} sub-areas, {atlas.Areas.Length:N0} areas, {atlas.Waypoints.Length:N0} zaaps");

            return atlas;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The world's tables do not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static long ZaapOf(int subAreaId, int mapId)
    {
        // Every sub-area without a zaap of client 3.7.12.12 names map 0; any other value that is no map is not measured.
        return mapId switch
        {
            > 0 => mapId,
            0 => SubArea.NoZaap,
            _ => throw new InvalidDataException($"Sub-area {subAreaId} names zaap map {mapId}."),
        };
    }

    /// <summary>Holds the graph and the atlas against the maps, failing on what contradicts what the extractor measured.</summary>
    public static void Check(WorldGraph graph, WorldAtlas atlas, IReadOnlyList<MapData> maps)
    {
        var byId = maps.ToDictionary(map => map.Id);

        foreach (var vertex in graph.Vertices.Span)
        {
            if (!byId.TryGetValue(vertex.MapId, out var map))
                throw new InvalidDataException($"The world graph names map {vertex.MapId}, which the client does not hold.");

            if (!ZoneIsOn(map, vertex.Zone))
                throw new InvalidDataException($"The world graph names zone {vertex.Zone} of map {vertex.MapId}, which no walkable cell has: a cell's zone may no longer be the high bits of its linkedZone.");
        }

        var interactives = 0;
        var elsewhere = 0;

        foreach (var edge in graph.Edges.Span)
        {
            var source = byId[graph.Vertices.Span[edge.From].MapId];

            foreach (var transition in graph.TransitionsOf(edge))
            {
                if (transition.Type is not WorldTransitionType.Interactive)
                    continue;

                if (!source.TryGetInteractive(transition.ElementId, out var element))
                    throw new InvalidDataException($"A transition of map {source.Id} uses element {transition.ElementId}, which the map does not hold.");

                interactives++;

                if (element.CellId != transition.CellId)
                    elsewhere++;
            }
        }

        foreach (var position in atlas.Positions.Span)
        {
            if (!byId.ContainsKey(position.MapId))
                throw new InvalidDataException($"The maps' information names map {position.MapId}, which the client does not hold.");
        }

        foreach (var waypoint in atlas.Waypoints.Span)
        {
            if (!graph.Contains(waypoint.MapId))
                throw new InvalidDataException($"Zaap {waypoint.Id} stands on map {waypoint.MapId}, which the world graph does not hold.");
        }

        Console.WriteLine($"Checked: every vertex's zone is on its map; {interactives:N0} interactive transitions use an element of their map, {elsewhere:N0} from another cell than the element's");
    }

    private static bool ZoneIsOn(MapData map, int zone)
    {
        foreach (var cell in map.Cells.Span)
        {
            if (cell.Properties.HasFlag(MapCellProperties.Walkable) && cell.Zone == zone)
                return true;
        }

        return false;
    }
}
