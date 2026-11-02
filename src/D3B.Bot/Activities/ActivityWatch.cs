namespace D3B.Bot.Activities;

/// <summary>A threshold an activity watches, from its settings, which the studio draws as a rule of the layer Always (D325).</summary>
/// <param name="Gauge">The gauge watched.</param>
/// <param name="Threshold">The threshold, a share of the gauge's most, from 0 to 100: the pods at or above it, the life at or under it.</param>
/// <param name="Then">What the activity does once the gauge reaches it.</param>
public sealed record ActivityWatch(WatchedGauge Gauge, int Threshold, WatchAction Then);
