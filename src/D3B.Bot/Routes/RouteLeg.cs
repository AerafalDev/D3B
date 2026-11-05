using System.Collections.Immutable;

namespace D3B.Bot.Routes;

/// <summary>The way from one anchor of a route to the next: the maps the world's path crosses between them (D299).</summary>
/// <param name="From">The map left.</param>
/// <param name="To">The map reached.</param>
/// <param name="Kind">What lies between them.</param>
/// <param name="Between">The maps the path crosses strictly between the two, in order; none when they are neighbours, are one map, or no path leads.</param>
public sealed record RouteLeg(long From, long To, RouteLegKind Kind, ImmutableArray<long> Between)
{
    /// <summary>Gets whether a path leads from <see cref="From"/> to <see cref="To"/>, or they are one map.</summary>
    public bool IsReachable => Kind != RouteLegKind.Unreachable;
}
