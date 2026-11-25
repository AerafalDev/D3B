using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Journal;
using D3B.Bot.Localization;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Activities;

/// <summary>The run's safety net (D519): an activity that failed, or made no progress a while, put back in order and begun anew, within bounds.</summary>
public sealed class ActivityRestartTests : IAsyncDisposable
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static string Say(GameText text)
    {
        return string.Format(_english, text.Format.In(_english), [.. text.Values.Select(value => value.Text ?? value.Id.ToString(_english))]);
    }

    [Fact]
    public void EachRestartOfTheHourWaitsTwiceAsLongAsTheOneBeforeEightMinutesAtMost()
    {
        var delays = Enumerable.Range(1, 7).Select(ActivityRestarts.Default.DelayOf).Select(delay => delay.TotalSeconds);

        Assert.Equal([30, 60, 120, 240, 480, 480, 480], delays);
    }

    [Fact]
    public void ARunWithNoProgressThenTheLongestWaitStillSendsARequestWithinTheServersThirtyMinutes()
    {
        // Measured (D481, D484): the server closes the connection 30 minutes after the bot's last request.
        Assert.True(ActivityRestarts.Default.NoProgress + ActivityRestarts.Default.LongestDelay + ActivityRestarts.Default.LookEvery < TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task AFailedStepIsFollowedByTheActivityBegunAnewOnceTheFirstDelayPassed()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity { Failure = step => step is 1 ? new InvalidOperationException("The step failed.") : null };
        var started = _play.Time.GetUtcNow();
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => activity.Started is 2, TimeSpan.FromSeconds(1));

        Assert.Equal(1, activity.Restarted);
        Assert.Equal(1, run.Restarts);
        Assert.Equal((1, TimeSpan.FromSeconds(30), "The step failed."), (run.LastRestart!.Restart, run.LastRestart.Delay, run.LastRestart.Cause.Message));
        Assert.True(_play.Time.GetUtcNow() - started >= TimeSpan.FromSeconds(30));
        Assert.False(run.IsOver);
        Assert.Contains(bot.Journal.All(), entry => entry is ActivityRestartedEntry { Restart: 1, Cause: "The step failed." });

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task TheCharacterIsPutBackInOrderBeforeTheActivityBeginsAnew()
    {
        var (connection, bot) = await _play.EnterAsync(433);
        var activity = new FailingActivity
        {
            Failure = step =>
            {
                // The step leaves a dialog open as it fails, as a banker's that the activity could not take to the bank.
                if (step is 1)
                    connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });

                return step is 1 ? new InvalidOperationException("The step failed.") : null;
            },
        };
        var run = bot.StartActivity(activity);

        Assert.Equal(new DialogLeaveRequest(), await connection.SentAsync(1));

        connection.Server(new DialogLeaveEvent());
        await _play.AdvanceUntilAsync(() => activity.Started is 2, TimeSpan.FromSeconds(1));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AFailureNoRestartWouldChangeEndsTheRunAtOnce()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity { Failure = _ => new ActivityException("The zone is gone.") { IsFinal = true } };
        var run = bot.StartActivity(activity);

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal((0, 0, false), (activity.Restarted, run.Restarts, run.GaveUp));
        Assert.Equal("The zone is gone.", run.Failure!.Message);
    }

    [Fact]
    public async Task AnActivityItsProfileDoesNotRestartFailsAtItsFirstFailure()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity { Restarts = false };
        var run = bot.StartActivity(activity);

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal((1, 0), (activity.Started, activity.Restarted));
    }

    [Fact]
    public async Task PastTheRestartsAnHourAllowsTheRunFailsSayingItGaveUp()
    {
        _play.Bots.Restarts = new ActivityRestarts { FirstDelay = TimeSpan.FromSeconds(1), MostPerHour = 3 };

        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity();
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => run.IsOver, TimeSpan.FromMilliseconds(500));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal((4, 3, 3, true), (activity.Started, activity.Restarted, run.Restarts, run.GaveUp));
        Assert.Contains(bot.Journal.All(), entry => entry is ActivityOverEntry { Outcome: ActivityOutcome.Failed, GaveUp: true });
    }

    [Fact]
    public async Task TheRestartsOfAnHourGoneByCountNoMore()
    {
        _play.Bots.Restarts = new ActivityRestarts { FirstDelay = TimeSpan.FromSeconds(1), MostPerHour = 1 };

        var (_, bot) = await _play.EnterAsync(300);

        // The first step fails; the second waits on purpose for 70 minutes, then makes no progress for 10: the hour's first restart again.
        var activity = new FailingActivity
        {
            Failure = step => step is 1 ? new InvalidOperationException("The step failed.") : null,
            Waiting = _play.Time.GetUtcNow() + TimeSpan.FromMinutes(70),
        };
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => activity.Started is 3, TimeSpan.FromSeconds(15));

        Assert.False(run.IsOver);
        Assert.Equal((2, 1), (run.Restarts, run.LastRestart!.Restart));
        Assert.True(_play.Time.GetUtcNow() - run.StartedAt >= TimeSpan.FromMinutes(80));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task ARunThatMakesNoProgressForTenMinutesIsRestartedSayingSo()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity { Failure = _ => null };
        var started = _play.Time.GetUtcNow();
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => activity.Started is 2, TimeSpan.FromSeconds(5));

        var waited = _play.Time.GetUtcNow() - started;

        Assert.True(waited >= TimeSpan.FromMinutes(10) && waited < TimeSpan.FromMinutes(11), $"Restarted after {waited}.");
        Assert.Equal("Nothing done for 10 minutes: no map changed, no fight, nothing harvested or banked.", Say(Assert.IsType<ActivityException>(run.LastRestart!.Cause).Reason!));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AMapChangedIsProgress()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity { Failure = _ => null };
        var run = bot.StartActivity(activity);

        for (var minute = 0; minute < 30; minute += 6)
        {
            await _play.AdvanceUntilAsync(() => _play.Time.GetUtcNow() - run.StartedAt >= TimeSpan.FromMinutes(minute + 6), TimeSpan.FromSeconds(5));
            connection.Server(new MapCurrentEvent { MapId = minute % 12 is 0 ? TestPlay.NorthMapId : TestPlay.MapId });
            await bot.ProcessedAsync();
        }

        Assert.Equal((1, 0), (activity.Started, run.Restarts));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AWaitOnPurposeIsNoLackOfProgress()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity { Failure = _ => null, Waiting = _play.Time.GetUtcNow() + TimeSpan.FromHours(1) };
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => _play.Time.GetUtcNow() - run.StartedAt >= TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(5));

        Assert.Equal((1, 0), (activity.Started, run.Restarts));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task LifeComingBackIsProgress()
    {
        // Measured (D153): a point each half second; back from a lost fight at a tenth of 3,000, the life takes 22 minutes to come
        // back whole, which the harvest waits for (D481).
        var (connection, bot) = await _play.EnterAsync(300);

        connection.Server(new LifeRegenEndEvent { Life = 300, MaxLife = 3_000 });
        connection.Server(new LifeRegenStartEvent { Interval = 5 });
        await bot.ProcessedAsync();

        var activity = new FailingActivity { Failure = _ => null };
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => _play.Time.GetUtcNow() - run.StartedAt >= TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(5));

        Assert.Equal((1, 0), (activity.Started, run.Restarts));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task ThePlayersHandIsNoLackOfProgress()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity { Failure = _ => null };
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => activity.Started is 1, TimeSpan.FromSeconds(1));
        bot.Pause();
        await _play.AdvanceUntilAsync(() => _play.Time.GetUtcNow() - run.StartedAt >= TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(5));

        Assert.Equal(0, run.Restarts);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task ARunStoppedWhileItWaitsToRestartIsStoppedNeverRestarted()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity();
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => run.Status is ActivityStatus.Restarting && run.LastRestart is not null, TimeSpan.FromMilliseconds(100));
        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Equal((1, 0), (activity.Started, activity.Restarted));
    }

    [Fact]
    public async Task ARunItsActivityStoppedOnAnInterruptionIsNeverRestarted()
    {
        // As the hunt's third fight lost in a row, or a profile stopping after a fight (D198): the activity's own stop, not a failure.
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new FailingActivity { Failure = _ => null, Response = InterruptionResponse.Stop };
        var run = bot.StartActivity(activity);

        await _play.AdvanceUntilAsync(() => activity.Started is 1, TimeSpan.FromSeconds(1));
        bot.Pause();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Equal((0, 0), (run.Restarts, activity.Restarted));
    }
}
