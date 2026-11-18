using Google.Protobuf;

namespace D3B.Bot.Runtime;

/// <summary>Holds a bot's requests until what they need of other bots holds, such as a master waiting for its followers (D432, D437).</summary>
/// <remarks>Every request a behavior sends goes through it first (<see cref="BotContext.SendAsync"/>); a gate lets at once what it does not hold.</remarks>
internal interface IActionGate
{
    /// <summary>Waits, within the behavior sending it, until a request may go.</summary>
    /// <param name="context">The behavior's context, through which the gate may send its own requests.</param>
    /// <param name="request">The request the behavior sends.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>A task that completes once the request may go.</returns>
    Task PassAsync(BotContext context, IMessage request, CancellationToken cancellationToken);

    /// <summary>Waits, within the behavior, as before an action the gate holds, such as before the walk to a group of monsters it attacks (D447).</summary>
    /// <param name="context">The behavior's context, through which the gate may send its own requests.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>A task that completes once the bot may act.</returns>
    Task GatherAsync(BotContext context, CancellationToken cancellationToken);
}
