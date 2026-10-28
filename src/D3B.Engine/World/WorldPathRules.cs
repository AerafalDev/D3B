using System.Collections.Frozen;
using D3B.Data.World;
using D3B.Engine.Criteria;

namespace D3B.Engine.World;

/// <summary>What a path through the world may follow, for one character: what its criteria are answered on, and the transitions it avoids.</summary>
public sealed record WorldPathRules
{
    /// <summary>Gets what the character's criteria are answered on: the place is each transition's own map, filled in by the pathfinder.</summary>
    /// <remarks>
    /// Its <see cref="CriterionFacts.Subscribed"/> also says where the character may go: a sub-area the client closes to
    /// accounts without a subscription is entered only when it is <see langword="true"/>.
    /// </remarks>
    public required CriterionFacts Facts { get; init; }

    /// <summary>Gets the transitions not to follow, by index in <see cref="WorldGraph.Transitions"/>, such as one that just failed.</summary>
    public IReadOnlySet<int> Banned { get; init; } = FrozenSet<int>.Empty;
}
