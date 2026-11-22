using System.Collections.Immutable;

namespace D3B.Bot.Harvesting;

/// <summary>A part of a map the character's own part cannot walk to, such as a mine's ledge, entered by a scroll from one side (D495).</summary>
/// <param name="MapId">The map.</param>
/// <param name="Side">The map's edge the part touches: 0 east, 2 south, 4 west, 6 north.</param>
/// <param name="NeighbourId">The map on that side, which the scroll leaves from.</param>
/// <param name="Arrivals">The cells of that edge a scroll comes onto, from which the part's wanted elements are reached.</param>
internal sealed record MapLevel(long MapId, int Side, long NeighbourId, ImmutableArray<int> Arrivals);
