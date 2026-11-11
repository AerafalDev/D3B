namespace D3B.Extractor.Graph;

/// <summary>The client's graph of the ways between maps: a vertex per zone of a map, an edge per way from one to another.</summary>
internal sealed class WorldGraph
{
    /// <summary>Gets the vertices in order of map and zone, each with the client's <c>m_uid</c>.</summary>
    public required IReadOnlyList<(ulong Uid, long MapId, int Zone)> Vertices { get; init; }

    /// <summary>Gets the edges in order of their vertices' uids, each with its transitions.</summary>
    public required IReadOnlyList<(ulong From, ulong To, IReadOnlyList<GraphTransition> Transitions)> Edges { get; init; }
}
