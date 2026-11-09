using System.Collections.Immutable;

namespace D3B.Studio.Routes;

/// <summary>The way from one anchor of a route to the next: the maps the world's path crosses between them (D299).</summary>
/// <param name="From">The map left.</param>
/// <param name="To">The map reached.</param>
/// <param name="Kind">What lies between them.</param>
/// <param name="Between">The maps the path crosses strictly between the two, in order, the zaaps it lands on included; none when they are neighbours, are one map, or no path leads.</param>
/// <param name="Zaaps">The maps the path teleports to, a zaap each, in order (D378); the last anchor's own map when the path ends by one.</param>
public sealed record RouteLeg(long From, long To, RouteLegKind Kind, ImmutableArray<long> Between, ImmutableArray<long> Zaaps = default)
{
    /// <summary>Gets the maps the path teleports to, none when it walks the whole way.</summary>
    public ImmutableArray<long> Teleports => Zaaps.IsDefault ? [] : Zaaps;

    /// <summary>Gets whether the path takes a zaap.</summary>
    public bool UsesZaap => !Teleports.IsEmpty;

    /// <summary>Gets whether a path leads from <see cref="From"/> to <see cref="To"/>, or they are one map.</summary>
    public bool IsReachable => Kind != RouteLegKind.Unreachable;
}
