using System.Collections.Immutable;

namespace D3B.Bot.Behaviors;

/// <summary>A behavior's states as a run shows them, and the one playing, for the studio's rail (D325).</summary>
/// <param name="Always">The states of the layer Always, in their order.</param>
/// <param name="States">The other states, in their order, each followed by those under it.</param>
/// <param name="Playing">The id of the leaf playing, or <see langword="null"/> while none holds.</param>
public sealed record BehaviorOutline(ImmutableArray<OutlineState> Always, ImmutableArray<OutlineState> States, string? Playing);
