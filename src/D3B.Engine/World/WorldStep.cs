using D3B.Data.World;

namespace D3B.Engine.World;

/// <summary>One map change of a path through the world: the transition to follow from a vertex of the graph to the next.</summary>
/// <param name="From">The vertex the step leaves, by index in <see cref="WorldGraph.Vertices"/>.</param>
/// <param name="To">The vertex it leads to.</param>
/// <param name="TransitionIndex">The transition to follow, by index in <see cref="WorldGraph.Transitions"/>.</param>
/// <param name="Transition">That transition.</param>
public readonly record struct WorldStep(int From, int To, int TransitionIndex, WorldTransition Transition);
