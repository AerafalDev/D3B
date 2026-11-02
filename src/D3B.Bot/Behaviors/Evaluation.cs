namespace D3B.Bot.Behaviors;

/// <summary>What a compiled behavior reads as it chooses a state or plays a node: the bot, and the run's variables.</summary>
/// <param name="View">What the conditions read.</param>
/// <param name="Variables">The run's variables.</param>
internal sealed record Evaluation(BehaviorView View, BehaviorVariables Variables);
