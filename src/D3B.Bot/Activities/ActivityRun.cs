using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Runtime;

namespace D3B.Bot.Activities;

/// <summary>An activity a bot runs: its steps one after another, and what becomes of it (D87).</summary>
/// <remarks>
/// <para>
/// Each step, and each unloading and recovery, is a behavior the bot starts once it can act: a game connection plays the
/// character, placed on a map and out of a fight, and the player has not taken it over. Until then the run waits, with no
/// delay: for the client's new connection to take the bot back (D65), for the player to resume it (D66), for a fight to
/// end (D88). Before each step, a full character is unloaded instead; still full after that, the activity fails.
/// </para>
/// <para>
/// An interruption cancels the step in progress. The activity says what it leads to (<see cref="BotActivity.OnInterrupted"/>);
/// to resume, the run waits until the bot can act, recovers the character (<see cref="BotActivity.RecoverAsync"/>), then goes
/// on with the next step. The host stopping ends the run, as <see cref="Stop"/> does.
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1001", Justification = "The stop's token source holds no timer, and nothing reads its wait handle; disposed, a stop asked once the run is over would throw.")]
public sealed class ActivityRun
{
    private const int NoInterruption = -1;

    private readonly BotActor _bot;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource<ActivityOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _status = (int)ActivityStatus.Waiting;
    private int _interruption = NoInterruption;
    private int _steps;
    private Exception? _failure;

    // Only the run's own sequence of behaviors reads and writes it.
    private bool _unloaded;

    /// <summary>Gets the activity.</summary>
    public BotActivity Activity { get; }

    /// <summary>Gets what the activity does now.</summary>
    public ActivityStatus Status => (ActivityStatus)Volatile.Read(ref _status);

    /// <summary>Gets the last interruption that cancelled a step, if any.</summary>
    public Interruption? LastInterruption => Volatile.Read(ref _interruption) is var reason and not NoInterruption ? (Interruption)reason : null;

    /// <summary>Gets the number of steps the activity ran to their end.</summary>
    public int Steps => Volatile.Read(ref _steps);

    /// <summary>Gets the error that made the activity fail, such as a script's (D103); none when it failed for a character still full.</summary>
    public Exception? Failure => Volatile.Read(ref _failure);

    /// <summary>Gets whether the activity is over.</summary>
    public bool IsOver => _completion.Task.IsCompleted;

    /// <summary>Gets a task that completes once the activity is over, with how it ended.</summary>
    public Task<ActivityOutcome> Completion => _completion.Task;

    /// <summary>Raised when what the activity does changes, from the thread that runs it: a handler must be quick, and must not throw.</summary>
    public event EventHandler<ActivityStatus>? StatusChanged;

    internal ActivityRun(BotActor bot, BotActivity activity)
    {
        _bot = bot;
        Activity = activity;
    }

    /// <summary>Stops the activity: the step in progress is cancelled, and no other starts.</summary>
    public void Stop()
    {
        // Cancelled off the caller's thread: the step's code runs where its token's callbacks run.
        _ = _stop.CancelAsync();
    }

    internal void Start()
    {
        _ = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        BotLog.ActivityStarted(_bot.Log, _bot.Key.CharacterId, Activity.Name);

        var outcome = await LoopAsync().ConfigureAwait(false);

        BotLog.ActivityOver(_bot.Log, _bot.Key.CharacterId, Activity.Name, Steps, outcome);

        // An activity holding what must be let go of, such as a script's Lua state, is done with once over.
        if (Activity is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, exception);
            }
        }

        SetStatus(ActivityStatus.Over);
        _completion.TrySetResult(outcome);
    }

    private async Task<ActivityOutcome> LoopAsync()
    {
        Interruption? interrupted = null;

        while (true)
        {
            Interruption cut;

            SetStatus(ActivityStatus.Waiting);

            try
            {
                if (interrupted is { } reason)
                {
                    await _bot.RunWhenReadyAsync((context, token) => RecoverAsync(context, reason, token), _stop.Token).ConfigureAwait(false);
                    interrupted = null;
                    SetStatus(ActivityStatus.Waiting);
                }

                switch (await _bot.RunWhenReadyAsync(StepAsync, _stop.Token).ConfigureAwait(false))
                {
                    case Next.Done:
                        return ActivityOutcome.Done;
                    case Next.StillFull:
                        BotLog.ActivityStillFull(_bot.Log, _bot.Key.CharacterId, Activity.Name);

                        return ActivityOutcome.Failed;
                }

                continue;
            }
            catch (BotInterruptedException exception) when (exception.Reason is not Interruption.Stopped)
            {
                cut = exception.Reason;
            }
            catch (BotInterruptedException)
            {
                return ActivityOutcome.Stopped;
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                return ActivityOutcome.Stopped;
            }
            catch (ObjectDisposedException exception) when (exception.ObjectName == typeof(BotActor).FullName)
            {
                return ActivityOutcome.Stopped;
            }
            catch (Exception exception)
            {
                BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, exception);
                Volatile.Write(ref _failure, exception);

                return ActivityOutcome.Failed;
            }

            interrupted = cut;
            Volatile.Write(ref _interruption, (int)cut);

            // An unloading cut short is no unloading: the next step tries it again.
            _unloaded = false;

            InterruptionResponse response;

            try
            {
                response = Activity.OnInterrupted(cut);
            }
            catch (Exception exception)
            {
                BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, exception);
                Volatile.Write(ref _failure, exception);

                return ActivityOutcome.Failed;
            }

            BotLog.ActivityInterrupted(_bot.Log, _bot.Key.CharacterId, Activity.Name, cut, response);

            if (response is InterruptionResponse.Stop)
                return ActivityOutcome.Stopped;
        }
    }

    private async Task<bool> RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        SetStatus(ActivityStatus.Recovering);
        await Activity.RecoverAsync(context, reason, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task<Next> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = context.State;

        if (Activity.IsFull(state))
        {
            if (_unloaded)
                return Next.StillFull;

            SetStatus(ActivityStatus.Unloading);
            BotLog.ActivityUnloading(_bot.Log, _bot.Key.CharacterId, Activity.Name, state.Weight, state.MaxWeight);
            _unloaded = true;

            return await Activity.UnloadAsync(context, cancellationToken).ConfigureAwait(false) ? Next.Step : Next.StillFull;
        }

        _unloaded = false;
        SetStatus(ActivityStatus.Stepping);

        var more = await Activity.StepAsync(context, cancellationToken).ConfigureAwait(false);

        Interlocked.Increment(ref _steps);

        return more ? Next.Step : Next.Done;
    }

    private void SetStatus(ActivityStatus status)
    {
        if (Interlocked.Exchange(ref _status, (int)status) != (int)status)
            StatusChanged?.Invoke(this, status);
    }

    private enum Next
    {
        Step,
        Done,
        StillFull,
    }
}
