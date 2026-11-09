using D3B.Bot.Characters;
using D3B.Bot.Network;
using D3B.Network.Relay;
using Google.Protobuf;

namespace D3B.Bot.Runtime;

/// <summary>What the bot's inbox holds: a message of its game connection, or a request that something happen on its loop.</summary>
internal abstract record BotInput
{
    internal sealed record Seen(GameSession Session, RelayDirection Side, IMessage Message) : BotInput;

    internal sealed record Expecting(GameExpectation Expectation) : BotInput;

    internal sealed record Starting(TaskCompletionSource<RunningBehavior> Started, BotReadiness Readiness, CancellationToken CancellationToken) : BotInput;

    internal sealed record Waiting(BotWaiter Waiter) : BotInput;

    internal sealed record Finished(RunningBehavior Behavior) : BotInput;

    internal sealed record Resuming : BotInput;

    internal sealed record Pausing(bool FromApp) : BotInput;

    internal sealed record Attached(GameSession Session, CharacterState State) : BotInput;

    internal sealed record Detached(GameSession Session) : BotInput;

    internal sealed record Stopping : BotInput;

    internal sealed record Settling(TaskCompletionSource Settled) : BotInput;

    internal sealed record Processing(TaskCompletionSource Processed) : BotInput;
}
