using System.Collections.Immutable;

namespace D3B.Studio.Behaviors;

/// <summary>A state's actions, on its canvas: short actions in order, with branches, variables and waits, wired (D297, D307).</summary>
/// <param name="Entry">The first node played, if any.</param>
/// <param name="Nodes">The nodes.</param>
/// <param name="Links">The wires, of execution and of data.</param>
public sealed record ActionGraph(string? Entry, ImmutableArray<GraphNode> Nodes, ImmutableArray<GraphLink> Links)
{
    /// <summary>Gets a canvas with nothing on it.</summary>
    public static ActionGraph Empty { get; } = new(null, [], []);
}
