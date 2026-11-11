using System.Text.Json;
using D3B.Extractor.Maps;
using D3B.Extractor.World;

namespace D3B.Extractor.Graph;

/// <summary>The world graph's tables, <c>world-graph-vertices.json</c> and <c>world-graph-edges.json</c>, and the checks that hold it against the maps.</summary>
/// <remarks>
/// The checks are the measures to repeat at each build (<c>main:docs/client.md</c>, "World graph"): a client that changes
/// what a field means, such as which bits of a cell's <c>linkedZone</c> name its zone, fails the extraction.
/// </remarks>
internal static class GraphTables
{
    private const ushort Walkable = 1;

    /// <summary>Reads the graph from the client, checks it, the positions and the zaaps against the maps, and writes it.</summary>
    /// <exception cref="InvalidDataException">The graph or a table contradicts the maps.</exception>
    public static void Extract(ClientInstall client, IReadOnlyList<MapData> maps, IReadOnlyList<MapPosition> positions, IReadOnlyList<Zaap> zaaps, DataFolder folder, TextWriter output)
    {
        var graph = WorldGraphReader.Read(client);
        var offGraph = Check(graph, maps.ToDictionary(map => (long)map.Id), positions, zaaps);

        folder.Write("world-graph-vertices.json", WriteVertices(graph));
        folder.Write("world-graph-edges.json", WriteEdges(graph));

        output.WriteLine($"World graph: {graph.Vertices.Count:N0} vertices, {graph.Edges.Count:N0} edges, {graph.Edges.Sum(edge => edge.Transitions.Count):N0} transitions, checked against the maps; {offGraph:N0} zaaps on a map the graph lacks");
    }

    public static byte[] WriteVertices(WorldGraph graph)
    {
        return JsonTable.Write(graph.Vertices, (json, vertex) =>
        {
            json.WriteNumber("uid", vertex.Uid);
            json.WriteNumber("mapId", vertex.MapId);
            json.WriteNumber("zone", vertex.Zone);
        });
    }

    public static byte[] WriteEdges(WorldGraph graph)
    {
        return JsonTable.Write(graph.Edges, (json, edge) =>
        {
            json.WriteNumber("from", edge.From);
            json.WriteNumber("to", edge.To);
            json.WriteStartArray("transitions");

            foreach (var transition in edge.Transitions)
            {
                json.WriteStartObject();
                json.WriteString("type", transition.Type);
                json.WriteNumber("direction", transition.Direction);
                json.WriteNumber("cellId", transition.CellId);
                WriteNumberOrNull(json, "skillId", transition.SkillId);
                json.WriteNumber("mapId", transition.MapId);
                WriteNumberOrNull(json, "elementId", transition.ElementId);

                if (transition.Criterion is { } criterion)
                {
                    json.WriteString("criterion", criterion);
                }
                else
                {
                    json.WriteNull("criterion");
                }

                json.WriteEndObject();
            }

            json.WriteEndArray();
        });
    }

    /// <summary>Holds the graph and the tables against the maps.</summary>
    /// <returns>How many zaaps stand on a map the graph lacks: 3.7.13.17 placed one where its graph does not reach yet.</returns>
    /// <exception cref="InvalidDataException">A vertex, transition, position or zaap names what the maps lack.</exception>
    public static int Check(WorldGraph graph, IReadOnlyDictionary<long, MapData> maps, IReadOnlyList<MapPosition> positions, IReadOnlyList<Zaap> zaaps)
    {
        var vertices = new Dictionary<ulong, long>();

        foreach (var (uid, mapId, zone) in graph.Vertices)
        {
            if (!maps.TryGetValue(mapId, out var map))
            {
                throw new InvalidDataException($"The world graph names map {mapId}, which the client does not hold.");
            }

            if (!Enumerable.Range(0, MapData.CellCount).Any(cell => (map.Flags[cell] & Walkable) is not 0 && map.LinkedZone[cell] >> 4 == zone))
            {
                throw new InvalidDataException($"The world graph names zone {zone} of map {mapId}, which no walkable cell has: a cell's zone may no longer be the high bits of its linkedZone.");
            }

            vertices.Add(uid, mapId);
        }

        foreach (var (from, _, transitions) in graph.Edges)
        {
            var source = maps[vertices[from]];

            foreach (var transition in transitions)
            {
                if (transition.ElementId is { } element && !source.Elements.Any(candidate => candidate.Id == element))
                {
                    throw new InvalidDataException($"A transition of map {source.Id} uses element {element}, which the map does not hold.");
                }
            }
        }

        foreach (var position in positions)
        {
            if (!maps.ContainsKey(position.Id))
            {
                throw new InvalidDataException($"The maps' information names map {position.Id}, which the client does not hold.");
            }
        }

        var onGraph = vertices.Values.ToHashSet();
        var offGraph = 0;

        foreach (var zaap in zaaps)
        {
            if (!maps.ContainsKey(zaap.MapId))
            {
                throw new InvalidDataException($"Zaap {zaap.Id} stands on map {zaap.MapId}, which the client does not hold.");
            }

            offGraph += onGraph.Contains(zaap.MapId) ? 0 : 1;
        }

        return offGraph;
    }

    private static void WriteNumberOrNull(Utf8JsonWriter json, string name, int? value)
    {
        if (value is { } number)
        {
            json.WriteNumber(name, number);
        }
        else
        {
            json.WriteNull(name);
        }
    }
}
