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

    /// <summary>Gets whether the bot leaves its readiness to the game: a follower in its master's fight in a group the game follows, which the server sets ready as its master is (D477).</summary>
    bool LeavesReadyToTheGame { get; }

    /// <summary>Gets whether the bot placed itself in its fight's placement and leaves its readiness to the game, so that its master waits for it no more (D477).</summary>
    bool IsPlaced { get; }

    /// <summary>Waits, within the AI's placement, until the followers that join the bot's fight are placed and ready, or placed when their readiness is the game's (D477).</summary>
    /// <param name="context">The AI's context, in the placement.</param>
    /// <param name="atMost">How long to wait at most.</param>
    /// <param name="cancellationToken">The AI's token.</param>
    /// <returns>A task that completes once they are, the placement is over, or the delay passed.</returns>
    Task PlacedAsync(BotContext context, TimeSpan atMost, CancellationToken cancellationToken);

    /// <summary>Says whether the bot placed itself in its fight's placement, leaving its readiness to the game; not placed as a fight's play begins and ends.</summary>
    /// <param name="placed">Whether it placed itself.</param>
    void Placed(bool placed);

    /// <summary>Says that the bot asked to join its master's fight, the server not having joined it: its options are off, and it says it is ready itself in that fight (D477).</summary>
    void JoinedByRequest();

    /// <summary>Says that the bot's play of a fight is over: not placed, nor joined by request.</summary>
    void FightOver();
}
