using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Journal;
using D3B.Bot.Localization;
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
/// <para>
/// An activity that <see cref="BotActivity.RestartsAfterFailure"/> is restarted rather than ended when a step fails, the character
/// cannot be unloaded, or the run sees no progress for a while (<see cref="ActivityRestarts.NoProgress"/>: no map changed, no fight
/// begun, no pod gained or banked, the activity waiting on nothing): the character is put back in order as after an interruption,
/// the run waits a delay that doubles each time, then the activity begins anew (<see cref="BotActivity.Restart"/>). A failure that
/// <see cref="ActivityException.IsFinal"/>, or one past <see cref="ActivityRestarts.MostPerHour"/> restarts in the hour, ends it
/// (D519). A run stopped, by the player or by the activity's own answer to an interruption, is never restarted.
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1001", Justification = "The stop's token source holds no timer, and nothing reads its wait handle; disposed, a stop asked once the run is over would throw.")]
public sealed class ActivityRun
{
    private const int NoInterruption = -1;
    private const long NotEnded = -1;

    private static readonly TimeSpan _hour = TimeSpan.FromHours(1);

    private readonly BotActor _bot;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource<ActivityOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _status = (int)ActivityStatus.Waiting;
    private int _interruption = NoInterruption;
    private readonly DateTimeOffset _startedAt;
    private long _endedAt = NotEnded;
    private int _steps;
    private Exception? _failure;
    private RunFights _fights = new();
    private ActivityRestart? _lastRestart;
    private int _restarts;

    // The token of the behavior the run waits on now, which the watch of its progress cancels once it sees none (D519).
    private CancellationTokenSource? _attempt;

    // Until when the run waits before a restart, which the watch takes for no lack of progress.
    private long _restingUntil = NotEnded;

    // Only the run's own sequence of behaviors reads and writes them.
    private bool _unloaded;
    private bool _stillFull;
    private bool _gaveUp;
    private int _fightsSeen;
    private readonly List<DateTimeOffset> _restartTimes = [];

    /// <summary>Gets the activity.</summary>
    public BotActivity Activity { get; }

    /// <summary>Gets the name the activity was started under, such as its profile's, if its starter gave one.</summary>
    public string? Title { get; }

    /// <summary>Gets the character as the activity found it, from which what it gained is told, such as its professions' experience (D146).</summary>
    public CharacterState StartedWith { get; }

    /// <summary>Gets what the activity does now.</summary>
    public ActivityStatus Status => (ActivityStatus)Volatile.Read(ref _status);

    /// <summary>Gets the last interruption that cancelled a step, if any.</summary>
    public Interruption? LastInterruption => Volatile.Read(ref _interruption) is var reason and not NoInterruption ? (Interruption)reason : null;

    /// <summary>Gets the number of steps the activity ran to their end.</summary>
    public int Steps => Volatile.Read(ref _steps);

    /// <summary>Gets when the activity started, by the bot's clock.</summary>
    public DateTimeOffset StartedAt => _startedAt;

    /// <summary>Gets when the activity ended, by the bot's clock, once it is over.</summary>
    public DateTimeOffset? EndedAt => Volatile.Read(ref _endedAt) is var ticks and not NotEnded ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

    /// <summary>Gets the fights the character fought while the activity ran, and what they gave it (D505).</summary>
    public RunFights Fights => Volatile.Read(ref _fights);

    /// <summary>Gets the error that made the activity fail, as it threw it; none when it failed for a character still full.</summary>
    public Exception? Failure => Volatile.Read(ref _failure);

    /// <summary>Gets the last restart of the activity, once its run restarted it after a failure (D519).</summary>
    public ActivityRestart? LastRestart => Volatile.Read(ref _lastRestart);

    /// <summary>Gets how many times the run restarted the activity (D519).</summary>
    public int Restarts => Volatile.Read(ref _restarts);

    /// <summary>Gets whether the activity is over.</summary>
    public bool IsOver => _completion.Task.IsCompleted;

    /// <summary>Gets whether the activity failed for a character still full once unloaded, which no error says.</summary>
    /// <remarks>Read once the run is over: the run writes it before it completes.</remarks>
    public bool IsStillFull => IsOver && _stillFull;

    /// <summary>Gets whether the activity failed once its run had restarted it as often as an hour allows (D519).</summary>
    /// <remarks>Read once the run is over: the run writes it before it completes.</remarks>
    public bool GaveUp => IsOver && _gaveUp;

    /// <summary>Gets a task that completes once the activity is over, with how it ended.</summary>
    public Task<ActivityOutcome> Completion => _completion.Task;

    /// <summary>Raised when what the activity does changes, from the thread that runs it: a handler must be quick, and must not throw.</summary>
    public event EventHandler<ActivityStatus>? StatusChanged;

    internal ActivityRun(BotActor bot, BotActivity activity, string? title = null)
    {
        _bot = bot;
        Activity = activity;
        Title = title;
        StartedWith = bot.State;
        _startedAt = bot.Time.GetUtcNow();
    }

    private static ActivityException NotUnloaded()
    {
        return new ActivityException("The character could not be unloaded, even once put back in order and out of its map.", GameText.Of(ActivityTexts.Text("NotUnloaded")));
    }

    // Measured on the owner's transfer of 2026-11-26 (D573): a run stopped from the app leaves open the bank it was in, and the
    // next run waited a minute there before its busy wait (D484) left it. A run is started by the player, so it takes the
    // character as it finds it: a storage, a dialog or an exchange then left at once, no step of this run having opened it. The
    // same for a walk or a use a stopped run left going (D576): its Sacrieur's next run waited 23 s on a walk the client never
    // confirmed, the stopped harvest no longer there to settle it.
    private static async Task<bool> BeginAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (context.State is { IsInDialog: true } or { IsWalking: true } or { IsUsing: true } or { IsWalkHeld: true })
            await Recovery.SettleAsync(context, cancellationToken).ConfigureAwait(false);

        return true;
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

    /// <summary>Counts a fight that ended while the activity ran, on the bot's loop, the one that writes them.</summary>
    /// <param name="end">How the fight ended for the character.</param>
    /// <param name="died">Whether the character's fighter was dead as it ended.</param>
    internal void Count(FightEnd end, bool died)
    {
        if (!IsOver)
            Volatile.Write(ref _fights, Fights.With(end, died));
    }

    internal void Watch()
    {
        if (IsOver)
            return;

        try
        {
            Activity.Watch(_bot);
        }
        catch (Exception exception)
        {
            // On the bot's loop, which must see the next message whatever an activity's watch does.
            BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, exception);
        }
    }

    private async Task RunAsync()
    {
        BotLog.ActivityStarted(_bot.Log, _bot.Key.CharacterId, Activity.Name);
        _bot.Journal.Add(new ActivityStartedEntry(_bot.Time.GetUtcNow(), Activity.Name));

        var outcome = await LoopAsync().ConfigureAwait(false);

        BotLog.ActivityOver(_bot.Log, _bot.Key.CharacterId, Activity.Name, Steps, outcome);
        _bot.Journal.Add(new ActivityOverEntry(_bot.Time.GetUtcNow(), Activity.Name, Steps, outcome, Failure?.Message, _stillFull, _gaveUp));

        // An activity is made for one start (D93): nothing but its run holds it, so what it holds is let go of once over.
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

        Volatile.Write(ref _endedAt, _bot.Time.GetUtcNow().UtcTicks);
        SetStatus(ActivityStatus.Over);
        _completion.TrySetResult(outcome);
    }

    private async Task<ActivityOutcome> LoopAsync()
    {
        using var watching = new CancellationTokenSource();

        // Watched from the start whatever the activity says now: a behavior says it of the activity its step plays, which it has
        // none of before its first step (D519).
        var watch = WatchProgressAsync(watching.Token);

        try
        {
            return await StepsAsync().ConfigureAwait(false);
        }
        finally
        {
            await watching.CancelAsync().ConfigureAwait(false);
            await watch.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            Interlocked.Exchange(ref _attempt, null)?.Dispose();
        }
    }

    private async Task<ActivityOutcome> StepsAsync()
    {
        Interruption? interrupted = null;
        var begun = false;

        _fightsSeen = _bot.FightsBegun;

        while (true)
        {
            Interruption? interruption = null;
            Exception? failure = null;
            var notUnloaded = false;
            var attempt = Attempt();

            SetStatus(ActivityStatus.Waiting);

            try
            {
                if (interrupted is { } reason)
                {
                    await _bot.RunWhenReadyAsync((context, token) => RecoverAsync(context, reason, token), attempt).ConfigureAwait(false);
                    interrupted = null;
                    SetStatus(ActivityStatus.Waiting);
                }

                if (!begun)
                {
                    await _bot.RunWhenReadyAsync(BeginAsync, attempt).ConfigureAwait(false);
                    begun = true;
                }

                switch (await _bot.RunWhenReadyAsync(StepAsync, attempt).ConfigureAwait(false))
                {
                    case Next.Done:
                        return ActivityOutcome.Done;
                    case Next.StillFull:
                        BotLog.ActivityStillFull(_bot.Log, _bot.Key.CharacterId, Activity.Name);
                        _stillFull = true;

                        return ActivityOutcome.Failed;
                    case Next.Step:
                        // Measured through the app: steps sent one after another went faster than the client showed them. The
                        // client's display time at least, fixed and the owner's to raise, is let pass after each (D154).
                        await Task.Delay(_bot.Delays.BetweenSteps, _bot.Time, attempt).ConfigureAwait(false);

                        continue;
                }

                failure = NotUnloaded();
                notUnloaded = true;
            }
            catch (BotInterruptedException exception) when (exception.Reason is not Interruption.Stopped)
            {
                interruption = exception.Reason;
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
            catch (OperationCanceledException) when (attempt.IsCancellationRequested)
            {
                // Only the watch of the run's progress cancels a behavior so, the run's stop aside.
                var minutes = (long)_bot.RestartsOf().NoProgress.TotalMinutes;

                failure = new ActivityException($"Nothing done for {minutes} minutes: no map changed, no fight, nothing harvested or banked.", GameText.Of(ActivityTexts.Text("NoProgress"), GameTextValue.Number(minutes)));
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (failure is not null)
            {
                if (await RestartAsync(failure, notUnloaded).ConfigureAwait(false) is { } ended)
                    return ended;

                // An unloading cut short is no unloading, and one that failed is tried again by the activity begun anew.
                _unloaded = false;

                continue;
            }

            // The step neither went on nor failed: an interruption cut it.
            var cut = interruption ?? throw new UnreachableException();

            interrupted = cut;
            _fightsSeen = _bot.FightsBegun;
            Volatile.Write(ref _interruption, (int)cut);

            // An unloading cut short is no unloading: the next step tries it again.
            _unloaded = false;

            InterruptionResponse response;
            GameText? note;

            try
            {
                response = Activity.OnInterrupted(cut);
                note = Activity.Describe(cut);
            }
            catch (Exception exception)
            {
                return Fail(exception, false);
            }

            BotLog.ActivityInterrupted(_bot.Log, _bot.Key.CharacterId, Activity.Name, cut, response);
            _bot.Journal.Add(new ActivityInterruptedEntry(_bot.Time.GetUtcNow(), Activity.Name, cut, response, note));

            // A fight the AI plays for the activity is answered once over: paused or stopped before, the AI would stop too.
            if (cut is Interruption.Fight && Activity.FightsItsFights)
            {
                try
                {
                    response = Activity.OnFightOver(await FoughtAsync().ConfigureAwait(false), response);
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
                    return Fail(exception, false);
                }
            }

            if (response is InterruptionResponse.Stop)
                return ActivityOutcome.Stopped;

            if (response is InterruptionResponse.Pause)
                _bot.PauseAfterStep();
        }
    }

    // The activity failed: restarted as its profile says, or ended. Null once restarted, else how the run ends.
    private async Task<ActivityOutcome?> RestartAsync(Exception failure, bool notUnloaded)
    {
        var now = _bot.Time.GetUtcNow();
        var restarts = _bot.RestartsOf();

        _restartTimes.RemoveAll(at => now - at >= _hour);

        if (!Activity.RestartsAfterFailure || failure is ActivityException { IsFinal: true })
            return Fail(failure, notUnloaded);

        if (_restartTimes.Count >= restarts.MostPerHour)
        {
            _gaveUp = true;

            return Fail(failure, notUnloaded);
        }

        _restartTimes.Add(now);

        var restart = new ActivityRestart(now, _restartTimes.Count, restarts.DelayOf(_restartTimes.Count), failure);

        Volatile.Write(ref _lastRestart, restart);
        Interlocked.Increment(ref _restarts);
        BotLog.ActivityRestarting(_bot.Log, _bot.Key.CharacterId, Activity.Name, restart.Restart, restart.Delay, failure);
        _bot.Journal.Add(new ActivityRestartedEntry(now, Activity.Name, restart.Restart, restart.Delay, failure.Message));
        SetStatus(ActivityStatus.Restarting);

        var attempt = Attempt();

        try
        {
            await _bot.RunWhenReadyAsync(PutInOrderAsync, attempt).ConfigureAwait(false);
        }
        catch (BotInterruptedException exception) when (exception.Reason is Interruption.Stopped)
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
        catch (Exception exception) when (exception is BotInterruptedException or OperationCanceledException)
        {
            // A fight, the player's hand or a new connection, which the next step waits for as after any; or no progress while the
            // character was put in order, which the restart is for anyway.
        }

        Volatile.Write(ref _restingUntil, (_bot.Time.GetUtcNow() + restart.Delay).UtcTicks);

        try
        {
            await Task.Delay(restart.Delay, _bot.Time, _stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            return ActivityOutcome.Stopped;
        }
        finally
        {
            Volatile.Write(ref _restingUntil, NotEnded);
        }

        try
        {
            Activity.Restart();
        }
        catch (Exception exception)
        {
            return Fail(exception, false);
        }

        return null;
    }

    private ActivityOutcome Fail(Exception failure, bool notUnloaded)
    {
        if (notUnloaded)
        {
            BotLog.ActivityStillFull(_bot.Log, _bot.Key.CharacterId, Activity.Name);
            _stillFull = true;
        }
        else
        {
            BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, failure);
            Volatile.Write(ref _failure, failure);
        }

        return ActivityOutcome.Failed;
    }

    // A token of its own for the next behavior the run waits on, cancelled by the run's stop or by the watch of its progress.
    private CancellationToken Attempt()
    {
        var attempt = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);

        Interlocked.Exchange(ref _attempt, attempt)?.Dispose();

        return attempt.Token;
    }

    // Looks at the bot every while: a map changed, a fight begun, pods gained or banked, life coming back are progress, and so is a
    // while when the activity could not act or waits on purpose. Measured (D153): a point each half second, so a character of 3,000
    // life back from a lost fight at half regains it for 12.5 minutes, which the harvest waits for whole (D481). None for NoProgress, the behavior the run waits on is cancelled, as a failure.
    private async Task WatchProgressAsync(CancellationToken cancellationToken)
    {
        var since = _bot.Time.GetUtcNow();
        var seen = Mark(since);

        while (!cancellationToken.IsCancellationRequested)
        {
            var restarts = _bot.RestartsOf();

            await Task.Delay(restarts.LookEvery, _bot.Time, cancellationToken).ConfigureAwait(false);

            var now = _bot.Time.GetUtcNow();
            var mark = Mark(now);

            if (mark != seen || IsExcused(now))
            {
                seen = mark;
                since = now;

                continue;
            }

            if (now - since < restarts.NoProgress)
                continue;

            since = now;
            BotLog.ActivityStalled(_bot.Log, _bot.Key.CharacterId, Activity.Name, restarts.NoProgress);

            try
            {
                Volatile.Read(ref _attempt)?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The behavior ended meanwhile, and the next one is watched from now on.
            }
        }
    }

    private (long? MapId, int Fights, int Weight, bool Fighting, int? Life) Mark(DateTimeOffset now)
    {
        var state = _bot.State;

        return (state.MapId, _bot.FightsBegun, state.Weight, state.IsFighting, _bot.LifeAt(now));
    }

    // The game connection gone, the player's hand, a fight, the wait before a restart, or the activity's own wait: none is the run stuck;
    // nor is an activity that would not restart, which no progress never ends.
    private bool IsExcused(DateTimeOffset now)
    {
        return !Activity.RestartsAfterFailure || !_bot.IsConnected || _bot.IsPaused || _bot.State.IsFighting
            || Volatile.Read(ref _restingUntil) > now.UtcTicks
            || Activity.WaitingUntil > now;
    }

    private async Task<FightEnd?> FoughtAsync()
    {
        // The fight that cut the step, counted as its context came: it may be over already, a short one, and its end is kept.
        var fight = _bot.FightsBegun;

        await _bot.WaitUntilAsync(bot => bot.State.Fight is not { Phase: not FightPhase.Ended } || !bot.IsConnected || bot.FightsBegun != fight, _stop.Token).ConfigureAwait(false);

        return _bot.LastEndedFight == fight ? _bot.LastFightEnd : null;
    }

    private async Task<bool> RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        SetStatus(ActivityStatus.Recovering);
        await Activity.RecoverAsync(context, reason, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task<bool> PutInOrderAsync(BotContext context, CancellationToken cancellationToken)
    {
        await Recovery.SettleAsync(context, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task<Next> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        // A protector attacks 2 ms after the harvest's end (D144), once the step that harvested is over and before the next
        // begins, so that no step was cut and the activity neither counted nor answered the fight (D266): a fight begun
        // between steps is the activity's as one that cut a step.
        if (_bot.FightsBegun != _fightsSeen)
            throw new BotInterruptedException(Interruption.Fight);

        var state = context.State;

        // A master's group banks together (D433): a follower full takes it to the bank as the master full does.
        var group = _bot.Bank is { } bank && bank.Takes(Activity) ? bank : null;

        if (Activity.IsFull(state) || group?.IsFull(Activity) == true)
        {
            if (_unloaded)
                return Next.StillFull;

            SetStatus(ActivityStatus.Unloading);
            BotLog.ActivityUnloading(_bot.Log, _bot.Key.CharacterId, Activity.Name, state.Weight, state.MaxWeight);
            _bot.Journal.Add(new ActivityUnloadingEntry(_bot.Time.GetUtcNow(), Activity.Name, state.Weight, state.MaxWeight));
            _unloaded = true;

            var unloaded = group is null
                ? await Activity.UnloadAsync(context, cancellationToken).ConfigureAwait(false)
                : await group.UnloadAsync(context, Activity, cancellationToken).ConfigureAwait(false);

            if (unloaded && group is not null)
                Activity.OnGroupUnloaded();

            return unloaded ? Next.Step : Next.NotUnloaded;
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
        NotUnloaded,
    }
}
