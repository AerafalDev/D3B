namespace D3B.Studio.Behaviors;

/// <summary>What an event reads: the bot before and after what it handled (D298).</summary>
/// <param name="Before">The bot as it was.</param>
/// <param name="After">The bot as it is.</param>
/// <remarks>A new connection starts the state over from what the server sends, so that what it changes is read as the connection's, not as a level gained or an item received.</remarks>
public sealed record BotChange(BehaviorView Before, BehaviorView After)
{
    /// <summary>Gets whether the character's state went on from the same connection, so that its changes are the game's.</summary>
    public bool IsSameConnection => Before.IsConnected && After.IsConnected && Before.State.Key == After.State.Key && Before.State.Level is not null;
}
