using D3B.Bot.Characters;
using D3B.Data.Maps;
using D3B.Network.Relay;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>What a behavior sees of its bot: the character's state, the game's data, the clock, and the game connection to act through (D9).</summary>
/// <remarks>
/// A behavior reads snapshots of the state and never changes it: the server's answers change it, through the bot's loop.
/// It sends the requests that start an action (D60), expecting their answers first (<see cref="Expect"/>).
/// </remarks>
public sealed class BotContext
{
    private readonly BotActor _bot;
    private readonly RunningBehavior _behavior;

    /// <summary>Gets the character's state now: a snapshot, which the next message the bot sees replaces.</summary>
    public CharacterState State => _bot.State;

    /// <summary>Gets the maps of the installed build's pack.</summary>
    public GameMaps Maps => _bot.Maps;

    /// <summary>Gets the clock every delay goes through.</summary>
    public TimeProvider Time => _bot.Time;

    /// <summary>Gets the bot's log.</summary>
    public ILogger Log => _bot.Log;

    internal BotContext(BotActor bot, RunningBehavior behavior)
    {
        _bot = bot;
        _behavior = behavior;
    }

    /// <summary>Expects a message, before sending the request it answers.</summary>
    /// <param name="match">Tells the message: the side that sent it, the message, and the state once it took the message in.</param>
    /// <returns>The expectation, to wait on once the request is sent.</returns>
    public GameExpectation Expect(Func<RelayDirection, IMessage, CharacterState, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        return _bot.Expect(_behavior, match);
    }

    /// <summary>Sends the game server a request through the game connection the behavior started on (D60).</summary>
    /// <param name="request">The readable request, whole.</param>
    /// <param name="cancellationToken">Stops waiting; a request not gone yet then never goes.</param>
    /// <returns>A task that completes once the request went.</returns>
    public Task SendAsync(IMessage request, CancellationToken cancellationToken)
    {
        return _behavior.Session.SendAsync(request, cancellationToken);
    }
}
