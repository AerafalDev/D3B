using D3B.Data.World;

namespace D3B.Engine.World;

/// <summary>One map change of a path through the world: the transition to follow from a vertex of the graph to the next, or a teleport.</summary>
/// <param name="From">The vertex the step leaves, by index in <see cref="WorldGraph.Vertices"/>.</param>
/// <param name="To">The vertex it leads to.</param>
/// <param name="TransitionIndex">The transition to follow, by index in <see cref="WorldGraph.Transitions"/>, or <see cref="Teleport"/>.</param>
/// <param name="Transition">That transition, or the default for a teleport.</param>
public readonly record struct WorldStep(int From, int To, int TransitionIndex, WorldTransition Transition)
{
    /// <summary>The <see cref="TransitionIndex"/> of a teleport from where the character stands to a zaap it knows, which the graph does not hold (D83).</summary>
    public const int Teleport = -1;

    /// <summary>Gets whether the step teleports the character to the zaap of the map it leads to.</summary>
    public bool IsTeleport => TransitionIndex == Teleport;
}
