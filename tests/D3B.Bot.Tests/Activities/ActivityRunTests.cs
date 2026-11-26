using D3B.Bot.Activities;
using D3B.Bot.Journal;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Activities;

public sealed class ActivityRunTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static Actor Self()
    {
        return new Actor
        {
            Id = TestPlay.CharacterId,
            Disposition = new ActorDisposition { CellId = 314 },
            Information = new ActorInformation { Fighter = new FightFighter { Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Challenger } } },
        };
    }

    private static FightResult Result(FightOutcome outcome, long experience, long kamas)
    {
        return new FightResult
        {
            Outcome = outcome,
            Fighter = new FightResultFighter
            {
                FighterId = TestPlay.CharacterId,
                Character = new FightResultCharacter { Data = { new FightResultData { Experience = new FightExperience { Gained = experience } } } },
            },
            Loot = new FightLoot { Kamas = kamas },
        };
    }

    private static void Fight(TestConnection connection, FightOutcome outcome, long experience, long kamas, bool dies = false)
    {
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new FightFighterShowEvent { Fighter = Self() });

        if (dies)
            connection.Server(new GameActionFightEvent { ActionId = 103, SourceId = -1, Death = new FightDeath { TargetId = TestPlay.CharacterId } });

        connection.Server(new FightEndEvent { Results = { Result(outcome, experience, kamas) } });
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task StepsRunOneAfterAnotherUntilTheActivityIsDone()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        Assert.Same(run, bot.Activity);
        Assert.Equal(1, await activity.SteppingAsync(bot));
        Assert.Equal(ActivityStatus.Stepping, run.Status);

        connection.Server(new KamasUpdateEvent { Kamas = 1 });

        Assert.Equal(2, await activity.SteppingAsync(bot));

        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(2, run.Steps);
        Assert.Equal(ActivityStatus.Over, run.Status);
        Assert.Null(run.LastInterruption);
    }

    [Fact]
    public async Task TheNextStepWaitsTheTimeBetweenTwoActions()
    {
        _play.Bots.Delays = BotDelays.Measured;

        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Server(new KamasUpdateEvent { Kamas = 1 });
        await WaitForAsync(() => run.Steps is 1);

        // The client's display time at least, measured: the next step starts once it passed. The pause starts a moment after
        // the count went up, so the clock then moves a step at a time: moved once, it could move before the pause began.
        _play.Time.Advance(BotDelays.Measured.BetweenSteps - TimeSpan.FromMilliseconds(1));
        await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.Equal(1, activity.Started);

        await _play.AdvanceUntilAsync(() => activity.Started == 2, TimeSpan.FromMilliseconds(50));

        Assert.Equal(2, await activity.SteppingAsync(bot));
        run.Stop();
        await run.Completion;
    }
    [Fact]
    public async Task OneActivityRunsAtATime()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var run = bot.StartActivity(new TestActivity());

        Assert.Throws<InvalidOperationException>(() => bot.StartActivity(new TestActivity()));

        run.Stop();
        await run.Completion;

        var next = new TestActivity();

        bot.StartActivity(next);
        await next.SteppingAsync(bot);
        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await bot.Activity!.Completion);
    }

    [Fact]
    public async Task AFullCharacterIsUnloadedBeforeTheNextStep()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity { Full = state => state.Load >= 0.9 };

        connection.Server(new InventoryWeightEvent { Weight = 7_000, MaxWeight = 7_590 });
        await bot.ProcessedAsync();

        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);

        Assert.Equal(1, activity.Unloads);

        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(1, run.Steps);
    }

    [Fact]
    public async Task ACharacterStillFullOnceUnloadedFailsTheActivity()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity { Full = _ => true, UnloadEmpties = false };

        connection.Server(new InventoryWeightEvent { Weight = 7_590, MaxWeight = 7_590 });

        var run = bot.StartActivity(activity);

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal(1, activity.Unloads);
        Assert.Equal(0, activity.Started);
    }

    [Fact]
    public async Task ThePlayerTakingOverHoldsTheActivityUntilTheBotIsResumedThenRecovers()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);
        await bot.ProcessedAsync();

        Assert.True(bot.IsPaused);
        Assert.Equal(1, activity.Started);
        Assert.Equal(Interruption.Player, run.LastInterruption);

        bot.Resume();

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Player], activity.Recoveries);

        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
    }

    [Fact]
    public async Task ThePauseFromTheAppCutsTheStepAsThePlayersHandThenTheActivityResumes()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        bot.Pause();
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);
        await bot.ProcessedAsync();

        Assert.True(bot.IsPaused);
        Assert.Equal(Interruption.Player, run.LastInterruption);
        Assert.Contains(bot.Journal.All(), entry => entry is BotEventEntry { Event: BotEvent.PausedFromApp });
        Assert.DoesNotContain(bot.Journal.All(), entry => entry is BotEventEntry { Event: BotEvent.PausedAfterFight });

        bot.Resume();

        Assert.Equal(2, await activity.SteppingAsync(bot));

        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
    }

    [Fact]
    public async Task TheRecoveryLeavesTheBankThePlayerLeftOpen()
    {
        var (connection, bot) = await _play.EnterAsync(396, TestPlay.InsideMapId);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Client(new NpcActionRequest { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId, Action = 3 });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);

        bot.Resume();

        Assert.IsType<DialogLeaveRequest>(await connection.SentAsync(1));

        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.False(bot.State.IsInDialog);
    }

    [Fact]
    public async Task ARunBegunInABankAStoppedRunLeftOpenLeavesItBeforeItsFirstStep()
    {
        // Measured on the owner's run of 2026-11-26 12:58 UTC: a transfer stopped from the app with the bank open, and the run
        // started a second later waited a whole minute in it before its busy wait put the character in order (D573).
        var (connection, bot) = await _play.EnterAsync(396, TestPlay.InsideMapId);
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage });
        await bot.ProcessedAsync();

        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        bot.Resume();

        Assert.IsType<DialogLeaveRequest>(await connection.SentAsync(1));
        Assert.Equal(0, activity.Started);

        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        Assert.Equal(1, await activity.SteppingAsync(bot));
        Assert.False(bot.State.IsInDialog);
        Assert.Equal(ActivityStatus.Stepping, run.Status);
    }

    [Fact]
    public async Task ARunBegunOnAWalkAStoppedRunLeftUnconfirmedSettlesItBeforeItsFirstStep()
    {
        // Measured on the owner's Sacrieur of 2026-11-26 16:56 UTC: its harvest stopped 0.7 s after a walk the server showed and
        // the client never confirmed, and the profile's next run sent nothing for 23 s, its busy wait waiting on the walk (D576).
        var (connection, bot) = await _play.EnterAsync(433);
        Assert.True(CellPathfinder.TryFind(_play.Maps.Get(TestPlay.MapId).Cells.Span, 433, 388, out var path));
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        await bot.ProcessedAsync();

        var activity = new TestActivity();
        var startedAt = _play.Time.GetUtcNow();
        bot.StartActivity(activity);

        bot.Resume();
        await _play.AdvanceUntilAsync(() => connection.Sent.Count > 0, TimeSpan.FromMilliseconds(250));

        Assert.IsType<MapMovementRequest>(connection.Sent[0]);
        Assert.Equal(0, activity.Started);
        Assert.True(_play.Time.GetUtcNow() - startedAt < BusyWait.Bound / 2);
    }

    [Fact]
    public async Task ADisconnectionHoldsTheActivityUntilTheClientsNewConnectionPlacesTheCharacter()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Close();
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);

        Assert.Equal(Interruption.Disconnected, run.LastInterruption);
        Assert.False(bot.IsPaused);

        var (again, _) = await _play.EnterAsync(301);

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Disconnected], activity.Recoveries);

        again.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
    }

    [Fact]
    public async Task AFightHoldsTheActivityUntilItEndsAndTheCharacterIsPlaced()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new MapCurrentEvent { MapId = 191_104_008 });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);

        Assert.Equal(Interruption.Fight, run.LastInterruption);

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        Assert.Equal(1, activity.Started);

        connection.Server(TestPlay.Information(358));

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Fight], activity.Recoveries);
    }

    [Fact]
    public async Task AFightBegunBetweenTwoStepsIsTheActivitysAsOneThatCutAStep()
    {
        // Measured in game (D266): a protector's fight began as the harvest's step was over, and the activity never knew it.
        _play.Bots.Delays = BotDelays.Measured;

        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Server(new KamasUpdateEvent { Kamas = 1 });
        await WaitForAsync(() => run.Steps is 1);

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(358));
        await bot.ProcessedAsync();
        await _play.AdvanceUntilAsync(() => activity.Started == 2, TimeSpan.FromMilliseconds(500));

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Fight], activity.Interruptions);
        Assert.Equal([Interruption.Fight], activity.Recoveries);
        Assert.Equal(Interruption.Fight, run.LastInterruption);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AnActivityMayPauseTheBotOnAFightWhichTheCardResumes()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity { Response = InterruptionResponse.Pause };
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await WaitForAsync(() => bot.IsPaused);

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(358));
        await bot.ProcessedAsync();

        // Over, the fight leaves the bot paused: nothing starts before the card resumes it.
        Assert.Equal(1, activity.Started);
        Assert.Equal(ActivityStatus.Waiting, run.Status);

        bot.Resume();

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Fight], activity.Recoveries);
    }
    [Fact]
    public async Task AnActivityMayEndOnAnInterruption()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity { Response = InterruptionResponse.Stop };
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId, KeyMovements = { 300, 301 } });

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Equal([Interruption.Player], activity.Interruptions);
        Assert.Empty(activity.Recoveries);
    }

    [Fact]
    public async Task StoppingTheRunCancelsItsStep()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Empty(activity.Interruptions);

        await bot.ProcessedAsync();

        Assert.False(bot.IsActing);
        Assert.False(bot.IsPaused);
    }

    [Fact]
    public async Task AStepThatThrowsFailsTheActivity()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var run = bot.StartActivity(new TestActivity { Throws = true });

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheBotStoppingEndsTheActivityWhetherItStepsOrWaits(bool stepping)
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);

        if (!stepping)
        {
            connection.Close();
            await WaitForAsync(() => run.Status is ActivityStatus.Waiting);
        }

        await bot.DisposeAsync();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Throws<ObjectDisposedException>(() => bot.StartActivity(new TestActivity()));
    }

    [Fact]
    public async Task ARunCountsItsCharactersFightsWonLostAndDeathsWithTheExperienceAndKamasTheyGave()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        // A fight before the run is not its own.
        Fight(connection, FightOutcome.Won, 1_000, 100);
        await bot.ProcessedAsync();

        var run = bot.StartActivity(new TestActivity());

        Assert.Equal(new RunFights(), run.Fights);

        Fight(connection, FightOutcome.Won, 77, 7);
        Fight(connection, FightOutcome.Won, 23, 3, dies: true);
        Fight(connection, FightOutcome.Lost, 0, 0, dies: true);
        await bot.ProcessedAsync();

        Assert.Equal(new RunFights { Won = 2, Lost = 1, Deaths = 2, Experience = 100, Kamas = 10 }, run.Fights);

        run.Stop();
        await run.Completion;

        // Nor one after it.
        Fight(connection, FightOutcome.Won, 1_000, 100);
        await bot.ProcessedAsync();

        Assert.Equal((2, 100L), (run.Fights.Won, run.Fights.Experience));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Polling.Deadline;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The activity never got there.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
