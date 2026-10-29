using System.Collections.Frozen;

namespace D3B.Bot.Harvesting;

/// <summary>What a bot saw of a map: the resources its elements gather, harvested or not, and when.</summary>
/// <param name="Resources">The resources, by item.</param>
/// <param name="At">When, by the bot's clock.</param>
public sealed record MapSighting(FrozenSet<int> Resources, DateTimeOffset At);
