namespace D3B.Extractor.Graph;

/// <summary>A way along an edge of the world graph, as <c>PathFindingData</c> holds it (<c>main:docs/client.md</c>, "World graph").</summary>
internal sealed class GraphTransition
{
    /// <summary>The four types the graph holds, by their <c>m_type</c>: one bit each, as measured on 3.7.12.12 and 3.7.13.17.</summary>
    public static readonly IReadOnlyDictionary<int, string> Types = new Dictionary<int, string>
    {
        [1] = "scroll",
        [2] = "scrollAction",
        [8] = "mapAction",
        [32] = "interactive",
    };

    /// <summary>Gets the type's name: walk off an edge, the same with an action, step on a cell, or use an element.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the direction of a scroll, or 255 for none.</summary>
    public required int Direction { get; init; }

    /// <summary>Gets the cell of the source map the transition is taken from.</summary>
    public required int CellId { get; init; }

    /// <summary>Gets the skill an interactive transition uses, or <see langword="null"/> for the others.</summary>
    public required int? SkillId { get; init; }

    /// <summary>Gets <c>m_transitionMapId</c>: the destination, but for a scroll with an action, the neighbour the client asks for.</summary>
    public required long MapId { get; init; }

    /// <summary>Gets the source map's element an interactive transition uses, or <see langword="null"/> for the others.</summary>
    public required int? ElementId { get; init; }

    /// <summary>Gets the criterion in the client's condition syntax, or <see langword="null"/> for none.</summary>
    public required string? Criterion { get; init; }
}
