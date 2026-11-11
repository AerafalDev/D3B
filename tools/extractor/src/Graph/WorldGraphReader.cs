using D3B.Extractor.Unity;

namespace D3B.Extractor.Graph;

/// <summary>Reads the client's world graph: the <c>PathFindingData</c> of <c>aa/&lt;platform&gt;/worldassets_assets_all.bundle</c>.</summary>
/// <remarks>
/// <c>m_vertices</c> holds, for each map, its vertices by zone; <c>m_edges</c>, for each source vertex by uid, its edges by
/// destination uid; <c>m_outgoingEdges</c> repeats the edges and is not read (<c>main:docs/client.md</c>, "World graph").
/// </remarks>
internal static class WorldGraphReader
{
    private const int NoId = -1;

    /// <summary>Finds and reads the graph in the client, whatever its platform's folder.</summary>
    /// <exception cref="InvalidDataException">The client has no graph, or it is not what the extractor measured.</exception>
    public static WorldGraph Read(ClientInstall client)
    {
        var root = Path.Combine(client.StreamingAssets, "aa");
        var paths = Directory.Exists(root) ? Directory.GetFiles(root, "worldassets_assets_all.bundle", SearchOption.AllDirectories) : [];

        if (paths.Length is not 1)
        {
            throw new InvalidDataException($"The client holds {paths.Length} worldassets_assets_all.bundle under {root}, where the extractor reads one.");
        }

        using var bundle = Bundle.Open(paths[0]);

        foreach (var graph in bundle.MonoBehaviours())
        {
            if (graph.Tree.Child(0, "m_vertices") is not -1 && graph.Tree.Child(0, "m_edges") is not -1)
            {
                return Read(graph);
            }
        }

        throw new InvalidDataException($"{paths[0]} holds no world graph.");
    }

    /// <summary>Reads a graph's object.</summary>
    /// <exception cref="InvalidDataException">The graph is not laid out as measured, or names a vertex it lacks.</exception>
    public static WorldGraph Read(SerializedObject graph)
    {
        var tree = graph.Tree;
        var reader = new ObjectReader(graph.Data, tree);
        var vertices = new List<(ulong Uid, long MapId, int Zone)>();
        var edges = new List<(ulong From, ulong To, IReadOnlyList<GraphTransition> Transitions)>();

        foreach (var child in tree[0].Children)
        {
            switch (tree[child].Field)
            {
                case "m_vertices":
                    ReadNested(ref reader, child, vertices, ReadVertices);
                    break;
                case "m_edges":
                    ReadNested(ref reader, child, edges, ReadEdges);
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.ExpectEnd();

        vertices.Sort((left, right) => (left.MapId, left.Zone).CompareTo((right.MapId, right.Zone)));

        var uids = new HashSet<ulong>();

        foreach (var vertex in vertices)
        {
            if (!uids.Add(vertex.Uid))
            {
                throw new InvalidDataException($"The world graph has two vertices of uid {vertex.Uid}.");
            }
        }

        foreach (var (from, to, transitions) in edges)
        {
            if (!uids.Contains(from) || !uids.Contains(to) || transitions.Count is 0)
            {
                throw new InvalidDataException($"The world graph's edge from {from} to {to} names a vertex it lacks, or has no transition.");
            }
        }

        return new WorldGraph { Vertices = vertices, Edges = [.. edges.OrderBy(edge => edge.From).ThenBy(edge => edge.To)] };
    }

    /// <summary>Reads a dictionary of <c>m_keys</c> and <c>m_values</c> whose values the item reader reads, key by key.</summary>
    private static void ReadNested<T>(ref ObjectReader reader, int node, List<T> into, ItemReader<T> read)
    {
        var (keys, values) = Parts(reader.Tree, node);
        var names = reader.ReadInt64Array(keys);
        var item = reader.BeginArray(values, out var count);

        if (count != names.Length)
        {
            throw new InvalidDataException($"{reader.Tree.Name}.{reader.Tree[node].Name}: {names.Length} keys for {count} values.");
        }

        for (var i = 0; i < count; i++)
        {
            read(ref reader, item, names[i], into);
            reader.AlignAfter(item);
        }

        reader.EndArray(values);
        reader.AlignAfter(node);
    }

    private static void ReadVertices(ref ObjectReader reader, int node, long mapId, List<(ulong Uid, long MapId, int Zone)> vertices)
    {
        var (keys, values) = Parts(reader.Tree, node);
        var zones = reader.ReadInt32Array(keys);
        var vertex = reader.BeginArray(values, out var count);

        if (count != zones.Length)
        {
            throw new InvalidDataException($"Map {mapId} has {zones.Length} zones for {count} vertices in the world graph.");
        }

        for (var i = 0; i < count; i++)
        {
            var read = ReadVertex(ref reader, vertex);

            if (read.MapId != mapId || read.Zone != zones[i])
            {
                throw new InvalidDataException($"The world graph's vertex of map {mapId}, zone {zones[i]}, names map {read.MapId}, zone {read.Zone}.");
            }

            vertices.Add(read);
        }

        reader.EndArray(values);
    }

    private static void ReadEdges(ref ObjectReader reader, int node, long source, List<(ulong From, ulong To, IReadOnlyList<GraphTransition> Transitions)> edges)
    {
        var tree = reader.Tree;
        var (keys, values) = Parts(tree, node);
        var destinations = reader.ReadInt64Array(keys);
        var edge = reader.BeginArray(values, out var count);

        if (count != destinations.Length)
        {
            throw new InvalidDataException($"Vertex {source} has {destinations.Length} destinations for {count} edges in the world graph.");
        }

        for (var i = 0; i < count; i++)
        {
            ulong from = 0, to = 0;
            var transitions = new List<GraphTransition>();

            foreach (var child in tree[edge].Children)
            {
                switch (tree[child].Field)
                {
                    case "m_from":
                        from = ReadVertex(ref reader, child).Uid;
                        break;
                    case "m_to":
                        to = ReadVertex(ref reader, child).Uid;
                        break;
                    case "m_transitions":
                        var item = reader.BeginArray(child, out var transitionCount);

                        for (var j = 0; j < transitionCount; j++)
                        {
                            transitions.Add(ReadTransition(ref reader, item));
                        }

                        reader.EndArray(child);
                        break;
                    default:
                        reader.Skip(child);
                        break;
                }
            }

            reader.AlignAfter(edge);

            if (from != (ulong)source || to != (ulong)destinations[i])
            {
                throw new InvalidDataException($"The world graph's edge from {source} to {destinations[i]} names {from} to {to}.");
            }

            edges.Add((from, to, transitions));
        }

        reader.EndArray(values);
    }

    private static (int Keys, int Values) Parts(TypeTree tree, int node)
    {
        var keys = tree.Child(node, "m_keys");
        var values = tree.Child(node, "m_values");

        return keys is -1 || values is -1 || tree[node].Children.Length is not 2 ? throw tree.Unexpected(node, "a dictionary of m_keys and m_values") : (keys, values);
    }

    private static (ulong Uid, long MapId, int Zone) ReadVertex(ref ObjectReader reader, int node)
    {
        var tree = reader.Tree;
        (ulong Uid, long MapId, int Zone) vertex = default;

        foreach (var child in tree[node].Children)
        {
            switch (tree[child].Field)
            {
                case "m_mapId":
                    vertex.MapId = reader.ReadInt64(child);
                    break;
                case "m_zoneId":
                    vertex.Zone = reader.ReadInt32(child);
                    break;
                case "m_uid":
                    vertex.Uid = reader.ReadUInt64(child);
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.AlignAfter(node);

        return vertex;
    }

    private static GraphTransition ReadTransition(ref ObjectReader reader, int node)
    {
        var tree = reader.Tree;
        int type = 0, direction = 0, skill = 0, cell = 0;
        long map = 0, element = 0;
        var criterion = "";

        foreach (var child in tree[node].Children)
        {
            switch (tree[child].Field)
            {
                case "m_type":
                    type = reader.ReadInt32(child);
                    break;
                case "m_direction":
                    direction = reader.ReadInt32(child);
                    break;
                case "m_skillId":
                    skill = reader.ReadInt32(child);
                    break;
                case "m_criterion":
                    criterion = reader.ReadString(child);
                    break;
                case "m_transitionMapId":
                    map = reader.ReadInt64(child);
                    break;
                case "m_cellId":
                    cell = reader.ReadInt32(child);
                    break;
                case "m_id":
                    element = reader.ReadInt64(child);
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.AlignAfter(node);

        if (!GraphTransition.Types.TryGetValue(type, out var name))
        {
            throw new InvalidDataException($"A transition to map {map} has type {type}, which the extractor does not know: measure it, then name it.");
        }

        var interactive = name is "interactive";

        // Directions run 0 to 7 clockwise from east, 255 for none; a transition is taken from one of its map's 560 cells.
        if (direction is (< 0 or > 7) and not 255 || cell is < 0 or >= Maps.MapData.CellCount)
        {
            throw new InvalidDataException($"A {name} transition to map {map} names direction {direction} and cell {cell}.");
        }

        // Only interactive transitions name a skill and an element: another type naming one is not what was measured.
        if (interactive ? skill < 0 || element is < 0 or > int.MaxValue : skill is not NoId || element is not NoId)
        {
            throw new InvalidDataException($"A {name} transition to map {map} names skill {skill} and element {element}.");
        }

        return new GraphTransition
        {
            Type = name,
            Direction = direction,
            CellId = cell,
            SkillId = interactive ? skill : null,
            MapId = map,
            ElementId = interactive ? (int)element : null,
            Criterion = criterion.Length is 0 ? null : criterion,
        };
    }

    private delegate void ItemReader<T>(ref ObjectReader reader, int node, long key, List<T> into);
}
