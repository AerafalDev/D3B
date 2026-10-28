using System.Threading.Channels;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Network;
using D3B.Data.Maps;
using D3B.Engine.World;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>A character D3B plays: an actor with one inbox, read on one logical thread, so its state needs no lock (D9).</summary>
/// <remarks>
/// <para>
/// One bot per character of an account (D65). Its inbox gets every message the character's game connection carries,
/// both sides, from the relay's read loops; its loop turns each into the next snapshot of the state
/// (<see cref="CharacterEvents"/>), which anyone reads. A bot outlives its game connections: when the client connects
/// again and chooses the character, the new connection takes the old one's place, and the state starts over from what
/// the server sends.
/// </para>
/// <para>
/// A behavior is an async method that reads snapshots and sends requests (<see cref="RunAsync"/>), one at a time. An
/// interruption cancels it: the game connection closing, the host stopping, or the player acting on the character. The
/// player wins: a request the player sends that acts on the character (<see cref="PlayerActions"/>) interrupts the bot's
/// behavior, and the bot then pauses, starting nothing until it is resumed (D66).
/// </para>
/// </remarks>
public sealed class BotActor : IAsyncDisposable
{
    /// <summary>How long the client takes to draw a map once the server placed the character on it (D77).</summary>
    internal static readonly TimeSpan DrawDelay = TimeSpan.FromMilliseconds(500);

    private readonly Channel<Input> _inbox = Channel.CreateUnbounded<Input>(new UnboundedChannelOptions { SingleReader = true });
    private readonly List<GameExpectation> _expectations = [];
    private readonly ILogger _log;
    private readonly Lazy<WorldPathfinder> _world;
    private readonly Lazy<BankDirectory> _banks;
    private readonly Task _loop;

    // Only the loop reads and writes these.
    private GameSession? _session;
    private RunningBehavior? _behavior;

    // The loop writes these, and anyone reads them.
    private volatile CharacterState _state;
    private volatile bool _connected;
    private volatile bool _paused;
    private volatile bool _acting;
    private long _drawnAt;

    /// <summary>Gets who the bot plays.</summary>
    public CharacterKey Key { get; }

    /// <summary>Gets the character's state now: an immutable snapshot, which the bot's loop replaces as messages come.</summary>
    public CharacterState State => _state;

    /// <summary>Gets whether a game connection plays the character now.</summary>
    public bool IsConnected => _connected;

    /// <summary>Gets whether the player took the character over from a behavior: the bot starts nothing until <see cref="Resume"/>.</summary>
    public bool IsPaused => _paused;

    /// <summary>Gets whether a behavior runs.</summary>
    public bool IsActing => _acting;

    internal GameMaps Maps { get; }

    internal WorldPathfinder World => _world.Value;

    internal BankDirectory Banks => _banks.Value;

    internal TimeProvider Time { get; }

    internal ILogger Log => _log;

    /// <summary>Gets when the client has drawn the map the server last placed the character on.</summary>
    internal DateTimeOffset DrawnAt => new(Volatile.Read(ref _drawnAt), TimeSpan.Zero);

    internal BotActor(CharacterState state, GameMaps maps, Lazy<WorldPathfinder> world, Lazy<BankDirectory> banks, TimeProvider time, ILogger log)
    {
        Key = state.Key;
        _state = state;
        Maps = maps;
        _world = world;
        _banks = banks;
        Time = time;
        _log = log;
        _loop = Task.Run(ReadInboxAsync);
    }

    /// <summary>Runs a behavior, the only one while it runs.</summary>
    /// <typeparam name="T">What the behavior gives.</typeparam>
    /// <param name="behavior">The behavior: an async method reading the bot's state and sending requests through its context.</param>
    /// <param name="cancellationToken">Stops the behavior.</param>
    /// <returns>What the behavior gave.</returns>
    /// <exception cref="InvalidOperationException">No game connection plays the character, the player took it over, or a behavior runs.</exception>
    /// <exception cref="BotInterruptedException">The player acted, the game connection closed, or the host is stopping.</exception>
    /// <exception cref="ObjectDisposedException">The bot was stopped.</exception>
    public async Task<T> RunAsync<T>(Func<BotContext, CancellationToken, Task<T>> behavior, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        var started = new TaskCompletionSource<RunningBehavior>(TaskCreationOptions.RunContinuationsAsynchronously);

        var posted = _inbox.Writer.TryWrite(new Starting(started, cancellationToken));

        ObjectDisposedException.ThrowIf(!posted, this);

        var running = await started.Task.ConfigureAwait(false);

        try
        {
            return await behavior(new BotContext(this, running), running.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (running.Reason is { } reason && exception is not BotInterruptedException)
        {
            // Whatever the behavior met once interrupted, such as a request that could not go, follows from the interruption.
            BotLog.Interrupted(_log, Key.CharacterId, reason);

            throw new BotInterruptedException(reason, exception);
        }
        finally
        {
            Post(new Finished(running));
        }
    }

    /// <summary>Lets the bot act again after the player took the character over.</summary>
    public void Resume()
    {
        Post(new Resuming());
    }

    /// <summary>Stops the bot: its behavior is interrupted, and its inbox read to the end.</summary>
    /// <returns>A task that completes once the bot's loop is over.</returns>
    public async ValueTask DisposeAsync()
    {
        Post(new Stopping());
        _inbox.Writer.TryComplete();
        await _loop.ConfigureAwait(false);
    }

    internal void Attach(GameSession session, CharacterState state)
    {
        Post(new Attached(session, state));
    }

    internal void See(GameSession session, RelayDirection side, IMessage message)
    {
        Post(new Seen(session, side, message));
    }

    internal void Detach(GameSession session)
    {
        Post(new Detached(session));
    }

    /// <summary>Waits until the loop handled everything posted before, so that a test reads the state they made.</summary>
    internal Task ProcessedAsync()
    {
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_inbox.Writer.TryWrite(new Processing(processed)))
            processed.TrySetResult();

        return processed.Task;
    }

    /// <summary>Has the loop take the walk's last cell as where the character stands, once the client did not confirm it (D85).</summary>
    internal Task SettleWalkAsync()
    {
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_inbox.Writer.TryWrite(new Settling(settled)))
            settled.TrySetResult();

        return settled.Task;
    }

    internal GameExpectation Expect(RunningBehavior owner, Func<RelayDirection, IMessage, CharacterState, bool> match)
    {
        var expectation = new GameExpectation(match, owner, Time);

        if (!_inbox.Writer.TryWrite(new Expecting(expectation)))
            expectation.Cancel();

        return expectation;
    }

    private void Post(Input input)
    {
        // Once the bot is stopped, what comes is dropped: no behavior runs any more to need it.
        _inbox.Writer.TryWrite(input);
    }

    private async Task ReadInboxAsync()
    {
        await foreach (var input in _inbox.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                Handle(input);
            }
            catch (Exception exception)
            {
                // A message the state handlers fail on must not stop the bot from seeing the next.
                BotLog.HandlerFailed(_log, Key.CharacterId, exception);
            }
        }
    }

    private void Handle(Input input)
    {
        switch (input)
        {
            case Seen seen:
                OnSeen(seen.Session, seen.Side, seen.Message);
                break;
            case Expecting expecting:
                OnExpecting(expecting.Expectation);
                break;
            case Starting starting:
                OnStarting(starting.Started, starting.CancellationToken);
                break;
            case Finished finished:
                OnFinished(finished.Behavior);
                break;
            case Resuming:
                OnResuming();
                break;
            case Attached attached:
                OnAttached(attached.Session, attached.State);
                break;
            case Detached detached:
                OnDetached(detached.Session);
                break;
            case Stopping:
                OnStopping();
                break;
            case Settling settling:
                _state = CharacterEvents.NotConfirmed(_state);
                settling.Settled.TrySetResult();
                break;
            case Processing processing:
                processing.Processed.TrySetResult();
                break;
        }
    }

    private void OnSeen(GameSession session, RelayDirection side, IMessage message)
    {
        if (session != _session)
            return;

        var state = CharacterEvents.Apply(_state, side, message);

        _state = state;

        if (side is RelayDirection.FromServer && message is MapInformationEvent information && information.MapId == state.MapId && state.Cell is not null)
            Volatile.Write(ref _drawnAt, (Time.GetUtcNow() + DrawDelay).UtcTicks);

        // A request to leave changes nothing of the state, so this is the dialog the client sent it in.
        if (side is RelayDirection.FromClient && _behavior is { } behavior && PlayerActions.Starts(state, message))
        {
            _paused = true;
            Interrupt(behavior, Interruption.Player);
            BotLog.PausedByPlayer(_log, Key.CharacterId, message.Descriptor.Name);
        }

        foreach (var expectation in _expectations)
            expectation.TryMatch(side, message, state);

        _expectations.RemoveAll(expectation => expectation.IsDone);
    }

    private void OnExpecting(GameExpectation expectation)
    {
        if (expectation.Owner == _behavior && expectation.Owner.Reason is null)
            _expectations.Add(expectation);
        else
            expectation.Cancel();
    }

    private void OnStarting(TaskCompletionSource<RunningBehavior> started, CancellationToken cancellationToken)
    {
        if (_session is not { } session)
        {
            started.TrySetException(new InvalidOperationException("No game connection plays the bot's character."));
        }
        else if (_paused)
        {
            started.TrySetException(new InvalidOperationException("The player took the character over: resume the bot first."));
        }
        else if (_behavior is not null)
        {
            started.TrySetException(new InvalidOperationException("The bot already runs a behavior."));
        }
        else
        {
            _behavior = new RunningBehavior(session, cancellationToken);
            _acting = true;
            started.TrySetResult(_behavior);
        }
    }

    private void OnFinished(RunningBehavior behavior)
    {
        if (behavior == _behavior)
        {
            CancelExpectations(behavior);
            _behavior = null;
            _acting = false;
        }

        behavior.Release();
    }

    private void OnResuming()
    {
        if (!_paused)
            return;

        _paused = false;
        BotLog.Resumed(_log, Key.CharacterId);
    }

    private void OnAttached(GameSession session, CharacterState state)
    {
        if (_behavior is { } behavior)
            Interrupt(behavior, Interruption.Disconnected);

        _session = session;
        _state = state;
        _connected = true;
        BotLog.Attached(_log, Key.CharacterId, session.Id);
    }

    private void OnDetached(GameSession session)
    {
        if (session != _session)
            return;

        if (_behavior is { } behavior)
            Interrupt(behavior, Interruption.Disconnected);

        _session = null;
        _connected = false;
        BotLog.Detached(_log, Key.CharacterId, session.Id);
    }

    private void OnStopping()
    {
        if (_behavior is { } behavior)
            Interrupt(behavior, Interruption.Stopped);

        _session = null;
        _connected = false;
    }

    private void Interrupt(RunningBehavior behavior, Interruption reason)
    {
        behavior.Interrupt(reason);
        CancelExpectations(behavior);
    }

    private void CancelExpectations(RunningBehavior behavior)
    {
        foreach (var expectation in _expectations.Where(expectation => expectation.Owner == behavior))
            expectation.Cancel();

        _expectations.RemoveAll(expectation => expectation.IsDone);
    }

    private abstract record Input;

    private sealed record Seen(GameSession Session, RelayDirection Side, IMessage Message) : Input;

    private sealed record Expecting(GameExpectation Expectation) : Input;

    private sealed record Starting(TaskCompletionSource<RunningBehavior> Started, CancellationToken CancellationToken) : Input;

    private sealed record Finished(RunningBehavior Behavior) : Input;

    private sealed record Resuming : Input;

    private sealed record Attached(GameSession Session, CharacterState State) : Input;

    private sealed record Detached(GameSession Session) : Input;

    private sealed record Stopping : Input;

    private sealed record Settling(TaskCompletionSource Settled) : Input;

    private sealed record Processing(TaskCompletionSource Processed) : Input;
}
