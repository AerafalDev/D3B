using D3B.Extractor.Tests.Unity;
using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;
using Transition = (int Type, int Direction, int Cell, int Skill, long Map, long Element, string Criterion);

namespace D3B.Extractor.Tests.Graph;

/// <summary>Builds a world graph's object by hand, a <c>PathFindingData</c> with its type tree and its bytes.</summary>
/// <remarks>
/// <c>m_vertices</c> holds, by map, the vertices by zone; <c>m_edges</c>, by source uid, the edges by destination uid; each
/// dictionary is its <c>m_keys</c> and its <c>m_values</c>. The shapes are ours, written from the extractor's reader.
/// </remarks>
internal sealed record TestGraph
{
    private const int NoId = -1;
    private const int NoDirection = 255;

    /// <summary>Gets the maps' entries: each its key, its zones' keys, and its vertices.</summary>
    public (long Map, int[] Zones, (long MapId, int Zone, ulong Uid)[] Vertices)[] Maps { get; init; } = [];

    /// <summary>Gets the keys of <c>m_vertices</c>, or null for the maps' own.</summary>
    public long[]? MapKeys { get; init; }

    /// <summary>Gets the sources' entries: each its uid, its destinations' keys, and its edges.</summary>
    public (long Source, long[] Destinations, (ulong From, ulong To, Transition[] Transitions)[] Edges)[] Sources { get; init; } = [];

    /// <summary>Gets whether <c>m_vertices</c> has a part besides its keys and values.</summary>
    public bool ThirdPart { get; init; }

    /// <summary>Gets bytes written after the object, which a reader must refuse.</summary>
    public byte[] Trailing { get; init; } = [];

    /// <summary>A graph of these vertices and edges, keyed as the client keys them.</summary>
    public static TestGraph Of((ulong Uid, long Map, int Zone)[] vertices, (ulong From, ulong To, Transition[] Transitions)[] edges)
    {
        return new TestGraph
        {
            Maps = [.. vertices.GroupBy(vertex => vertex.Map).Select(map => (map.Key, map.Select(vertex => vertex.Zone).ToArray(), map.Select(vertex => (vertex.Map, vertex.Zone, vertex.Uid)).ToArray()))],
            Sources = [.. edges.GroupBy(edge => edge.From).Select(source => ((long)source.Key, source.Select(edge => (long)edge.To).ToArray(), source.ToArray()))],
        };
    }

    public static Transition Scroll(long map, int direction, int cell)
    {
        return (1, direction, cell, NoId, map, NoId, "");
    }

    public static Transition MapAction(long map, int cell)
    {
        return (8, NoDirection, cell, NoId, map, NoId, "");
    }

    public static Transition Use(long map, int cell, int skill, long element, string criterion)
    {
        return (32, NoDirection, cell, skill, map, element, criterion);
    }

    private static IEnumerable<FlatNode> Vertex(int level, string name)
    {
        yield return Node(level, "Vertex", name, align: true);
        yield return Node(level + 1, "SInt64", "m_mapId", 8);
        yield return Node(level + 1, "int", "m_zoneId", 4);
        yield return Node(level + 1, "UInt64", "m_uid", 8);
    }

    private static IEnumerable<FlatNode> Values(int level, string item)
    {
        yield return Node(level, "vector", "m_values");
        yield return Array(level + 1, align: true);
        yield return Node(level + 2, "int", "size", 4);
        yield return Node(level + 2, item, "data", align: true);
    }

    private static void Keys(ObjectBytes bytes, long[] keys)
    {
        bytes.Int32(keys.Length);

        foreach (var key in keys)
        {
            bytes.Int64(key);
        }
    }

    private static void WriteVertex(ObjectBytes bytes, (long MapId, int Zone, ulong Uid) vertex)
    {
        bytes.Int64(vertex.MapId);
        bytes.Int32(vertex.Zone);
        bytes.Int64((long)vertex.Uid);
    }

    public SerializedObject Build()
    {
        List<FlatNode> nodes =
        [
            Node(0, "PathFindingData", "Base"),
            Node(1, "VerticesByMap", "m_vertices"),
            .. Vector(2, "m_keys", "SInt64", 8),
            .. Values(2, "VerticesByZone"),
            .. Vector(5, "m_keys", "int", 4),
            .. Values(5, "Vertex"),
            .. Vertex(7, "data").Skip(1),
            .. ThirdPart ? (FlatNode[])[Node(2, "int", "m_count", 4)] : [],
            Node(1, "EdgesBySource", "m_edges"),
            .. Vector(2, "m_keys", "SInt64", 8),
            .. Values(2, "EdgesByDestination"),
            .. Vector(5, "m_keys", "SInt64", 8),
            .. Values(5, "Edge"),
            .. Vertex(8, "m_from"),
            .. Vertex(8, "m_to"),
            Node(8, "vector", "m_transitions"),
            Array(9, align: true),
            Node(10, "int", "size", 4),
            Node(10, "Transition", "data", align: true),
            Node(11, "int", "m_type", 4),
            Node(11, "int", "m_direction", 4),
            Node(11, "int", "m_skillId", 4),
            .. String(11, "m_criterion"),
            Node(11, "SInt64", "m_transitionMapId", 8),
            Node(11, "int", "m_cellId", 4),
            Node(11, "SInt64", "m_id", 8),
            Node(8, "float", "m_cost", 4),
            .. Vector(1, "m_outgoingEdges", "SInt64", 8),
        ];
        var bytes = new ObjectBytes();
        var vertices = Maps.SelectMany(map => map.Vertices).GroupBy(vertex => vertex.Uid).ToDictionary(uid => uid.Key, uid => uid.First());

        Keys(bytes, MapKeys ?? [.. Maps.Select(map => map.Map)]);
        bytes.Int32(Maps.Length);

        foreach (var (_, zones, values) in Maps)
        {
            bytes.Int32(zones.Length);

            foreach (var zone in zones)
            {
                bytes.Int32(zone);
            }

            bytes.Int32(values.Length);

            foreach (var vertex in values)
            {
                WriteVertex(bytes, vertex);
            }
        }

        Keys(bytes, [.. Sources.Select(source => source.Source)]);
        bytes.Int32(Sources.Length);

        foreach (var (_, destinations, edges) in Sources)
        {
            Keys(bytes, destinations);
            bytes.Int32(edges.Length);

            foreach (var (from, to, transitions) in edges)
            {
                WriteVertex(bytes, vertices.GetValueOrDefault(from, (0, 0, from)));
                WriteVertex(bytes, vertices.GetValueOrDefault(to, (0, 0, to)));
                bytes.Int32(transitions.Length);

                foreach (var transition in transitions)
                {
                    bytes.Int32(transition.Type);
                    bytes.Int32(transition.Direction);
                    bytes.Int32(transition.Skill);
                    bytes.String(transition.Criterion);
                    bytes.Int64(transition.Map);
                    bytes.Int32(transition.Cell);
                    bytes.Int64(transition.Element);
                }

                bytes.Int32(0);
            }
        }

        bytes.Int32(0);
        bytes.Raw(Trailing);

        return new SerializedObject { PathId = 1, Name = "", Tree = TypeTree.Build("PathFindingData", nodes), Data = bytes.ToArray(), ReferenceTypes = new Dictionary<string, TypeTree>() };
    }
}
