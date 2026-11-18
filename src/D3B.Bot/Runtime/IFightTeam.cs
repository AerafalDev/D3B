namespace D3B.Bot.Runtime;

/// <summary>A bot's part in its group's fights, which the AI asks during the placement (D431, D434).</summary>
/// <remarks>
/// A master leaves its fight open to its party and says it is ready once its followers are placed; a follower leaves the
/// options of its team to its master. A bot of no group plays as before.
/// </remarks>
internal interface IFightTeam
{
    /// <summary>Gets the bot's part now.</summary>
    FightRole Role { get; }

    /// <summary>Waits, within the AI's placement, until the followers that join the bot's fight are placed and ready.</summary>
    /// <param name="context">The AI's context, in the placement.</param>
    /// <param name="atMost">How long to wait at most.</param>
    /// <param name="cancellationToken">The AI's token.</param>
    /// <returns>A task that completes once they are, the placement is over, or the delay passed.</returns>
    Task PlacedAsync(BotContext context, TimeSpan atMost, CancellationToken cancellationToken);
}
