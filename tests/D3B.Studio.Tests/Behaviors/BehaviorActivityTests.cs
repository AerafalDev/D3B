using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Protocol.Game;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using static D3B.Studio.Tests.Behaviors.TestBehaviors;

namespace D3B.Studio.Tests.Behaviors;

public sealed class BehaviorActivityTests : IAsyncDisposable
{
    private readonly TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    [Fact]
    public async Task AProfileOfAGivenBehaviorPlaysItsActivityWithTheValuesItChangesThenIsDone()
    {
        await _behaviors.PreparedAsync();

        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = _behaviors.Assets.Find<BehaviorAsset>(GivenAssets.BehaviorOf(SettingsActivityId))!;
        var start = _behaviors.Starter.Start(bot, ProfileOf(behavior, ("Walks", 4)));

        Assert.Null(start.Failure);
        Assert.Equal(ActivityOutcome.Done, await start.Run!.Completion);

        var activity = Assert.IsType<SettingsActivity>(Assert.Single(_behaviors.Activities));

        Assert.Equal(4, activity.Settings.Walks);
        Assert.Equal(["Settings"], States(bot));
    }

    [Fact]
    public async Task TheFirstStateWhoseConditionsHoldIsChosenAgainAtEachStepAsTheStateChanges()
    {
        var (connection, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("rich", "Rich") { When = When("kamas.atLeast", ("Amount", 10)), Repeat = StepRepeat.Forever, Actions = Chain(Note("r", "rich"), Node("w", "wait", ("Seconds", 60))) },
            new BehaviorState("poor", "Poor") { Repeat = StepRepeat.Forever, Actions = Chain(Note("p", "poor"), Node("v", "wait", ("Seconds", 60))) }));

        await UntilAsync(() => Notes(bot).Count == 1);
        connection.Server(new KamasUpdateEvent { Kamas = 10 });
        await bot.ProcessedAsync();
        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(60));
        await UntilAsync(() => Notes(bot).Count == 2);

        Assert.Equal(["poor", "rich"], Notes(bot));
        Assert.Equal(["Poor", "Rich"], States(bot));
        run.Stop();
        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task WhatWasPlayedIsPassedAndAStatePlayedToItsEndAndChosenAgainIsDone()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("only", "Only") { Actions = Chain(Note("a", "a"), Node("w", "wait", ("Seconds", 5)), Note("b", "b")) }));

        await UntilAsync(() => Notes(bot).Count == 1);
        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["a", "b"], Notes(bot));
        Assert.Equal(3, run.Steps);
    }

    [Fact]
    public async Task AnActionAnInterruptionCutIsPlayedAgainButNotWhatWasPlayedBefore()
    {
        var (connection, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("only", "Only") { Actions = Chain(Note("a", "a"), Node("w", "wait", ("Seconds", 60)), Note("b", "b")) }));

        await UntilAsync(() => Notes(bot).Count == 1 && run.Status is ActivityStatus.Stepping);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await UntilAsync(() => bot.IsPaused && run.Status is ActivityStatus.Waiting);
        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(60));
        bot.Resume();
        await UntilAsync(() => run.Status is ActivityStatus.Stepping);

        // The wait the player cut starts again whole: a minute more, then the way goes on.
        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(60));

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["a", "b"], Notes(bot));
        Assert.Equal(Interruption.Player, run.LastInterruption);
    }

    [Fact]
    public async Task AStateThatRepeatsPlaysItsActionsAgain()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("loop", "Loop") { Repeat = StepRepeat.Forever, Actions = Chain(Note("a", "again"), Node("w", "wait", ("Seconds", 1))) }));

        await UntilAsync(() => Notes(bot).Count == 1);
        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(1));
        await UntilAsync(() => Notes(bot).Count == 2);
        run.Stop();
        await run.Completion;

        Assert.Equal(["again", "again"], Notes(bot));
    }

    [Fact]
    public async Task AVariableSetChoosesAnotherState()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = Behavior(
            new BehaviorState("first", "First") { When = When("flag.off", ("First", Variable("done"))), Actions = Chain(Note("a", "first"), Node("set", NodeCatalog.SetVariable, ("variable", "done"), ("value", true))) },
            new BehaviorState("second", "Second") { Actions = Chain(Note("b", "second")) }) with
        {
            Variables = [new BehaviorVariable("done", VariableType.Flag, VariableScope.Run)],
        };
        var run = await _behaviors.StartAsync(bot, behavior);

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["first", "second"], Notes(bot));
        Assert.Equal(["First", "Second"], States(bot));
    }

    [Fact]
    public async Task ARuleOnAnEventPlaysAtOnceAndAnUrgentStateCutsTheStepRunning()
    {
        var (connection, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = Behavior(
            new BehaviorState("moved", "Moved") { When = When("flag.on", ("First", Variable("moved"))), IsUrgent = true, Actions = Chain(Note("m", "moved"), Node("stop", "stop")) },
            new BehaviorState("idle", "Idle") { Actions = Chain(Node("w", "wait", ("Seconds", 600))) }) with
        {
            Variables = [new BehaviorVariable("moved", VariableType.Flag, VariableScope.Run)],
            Rules = [new BehaviorRule("on-map", "On map", new NodeCall("map.changed", Values())) { Actions = Chain(Node("set", NodeCatalog.SetVariable, ("variable", "moved"), ("value", true))) }],
        };
        var run = await _behaviors.StartAsync(bot, behavior);

        await UntilAsync(() => States(bot).Count == 1 && run.Status is ActivityStatus.Stepping);
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(300, TestPlay.NorthMapId));

        // No time passes: the wait of ten minutes is cut as the rule sets the variable the urgent state reads.
        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["moved"], Notes(bot));
        Assert.Equal(["Idle", "Moved"], States(bot));
    }

    [Fact]
    public async Task ACharacterVariableIsKeptByCharacterAndName()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = Behavior(new BehaviorState("count", "Count") { Actions = Chain(Node("set", NodeCatalog.SetVariable, ("variable", "count"), ("value", 5))) }) with
        {
            Variables = [new BehaviorVariable("count", VariableType.Number, VariableScope.Character, 1)],
        };
        var run = await _behaviors.StartAsync(bot, behavior);

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(5, _behaviors.Variables.Get(TestPlay.Key, "count")?.GetValue<int>());
    }

    [Fact]
    public async Task APauseWaitsForTheBotToBeResumedThenTheWayGoesOn()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(new BehaviorState("pause", "Pause") { Actions = Chain(Node("p", "pause"), Note("b", "after")) }));

        await UntilAsync(() => bot.IsPaused && run.Status is ActivityStatus.Waiting);

        Assert.Empty(Notes(bot));

        bot.Resume();

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["after"], Notes(bot));
    }

    [Fact]
    public async Task WhenNoStateHoldsTheRunWaitsForOne()
    {
        var (connection, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("rich", "Rich") { When = When("kamas.atLeast", ("Amount", 10)), Actions = Chain(Note("r", "rich")) }));

        await UntilAsync(() => States(bot).Count == 1 && run.Status is ActivityStatus.Stepping);

        Assert.Equal([(string?)null], States(bot));

        connection.Server(new KamasUpdateEvent { Kamas = 10 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["rich"], Notes(bot));
    }

    [Fact]
    public async Task ABranchChoosesItsWayByAConditionWiredToIt()
    {
        var (connection, bot) = await _behaviors.Play.EnterAsync(300);

        connection.Server(new KamasUpdateEvent { Kamas = 50 });
        await bot.ProcessedAsync();

        var graph = new ActionGraph(
            "if",
            [
                Node("if", NodeCatalog.Branch),
                Node("rich", "kamas.atLeast", ("Amount", 10)),
                Note("yes", "yes"),
                Note("no", "no"),
            ],
            [
                new GraphLink("rich", GraphPins.Value, "if", "Condition"),
                new GraphLink("if", GraphPins.True, "yes"),
                new GraphLink("if", GraphPins.False, "no"),
            ]);
        var run = await _behaviors.StartAsync(bot, Behavior(new BehaviorState("only", "Only") { Actions = graph }));

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["yes"], Notes(bot));
    }

    [Fact]
    public async Task AValueBoundToAPublicSettingTakesTheProfilesValue()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = Behavior(new BehaviorState("only", "Only") { Actions = Chain(Node("n", "note", ("Text", Parameter("text")))) }) with
        {
            Parameters = [new BehaviorParameter("text", JsonValue.Create("behavior's"))],
        };
        var run = await _behaviors.StartAsync(bot, behavior, ("text", "profile's"));

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["profile's"], Notes(bot));
    }

    [Fact]
    public async Task TheTraceSaysTheStateChosenAndEachStepPlayedToItsEndForOneRun()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("only", "Only") { Actions = Chain(Note("a", "a"), Node("w", "wait", ("Seconds", 5))) }));

        await UntilAsync(() => Notes(bot).Count == 1);
        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(5));
        await run.Completion;

        var trace = bot.Trace.All();

        Assert.Equal([(TraceKind.Entered, TraceOutcome.None), (TraceKind.Played, TraceOutcome.Done), (TraceKind.Played, TraceOutcome.Done)], trace.Select(line => (line.Kind, line.Outcome)));
        Assert.All(trace, line => Assert.Equal("Only", line.State));
        Assert.Single(trace.Select(line => line.Run).Distinct());
        Assert.All(trace.Skip(1), line => Assert.NotNull(line.Node));
    }

    [Fact]
    public async Task TheTraceSaysARulePlayedAnUrgentCutAndTheStepItInterrupted()
    {
        var (connection, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = Behavior(
            new BehaviorState("moved", "Moved") { When = When("flag.on", ("First", Variable("moved"))), IsUrgent = true, Actions = Chain(Node("stop", "stop")) },
            new BehaviorState("idle", "Idle") { Actions = Chain(Node("w", "wait", ("Seconds", 600))) }) with
        {
            Variables = [new BehaviorVariable("moved", VariableType.Flag, VariableScope.Run)],
            Rules = [new BehaviorRule("on-map", "On map", new NodeCall("map.changed", Values())) { Actions = Chain(Node("set", NodeCatalog.SetVariable, ("variable", "moved"), ("value", true))) }],
        };
        var run = await _behaviors.StartAsync(bot, behavior);

        await UntilAsync(() => States(bot).Count == 1 && run.Status is ActivityStatus.Stepping);
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(300, TestPlay.NorthMapId));
        await run.Completion;

        var trace = bot.Trace.All();

        Assert.Contains(trace, line => line is { Kind: TraceKind.Rule, Outcome: TraceOutcome.Played, Subject: "On map", State: "Idle" });
        Assert.Contains(trace, line => line is { Kind: TraceKind.Urgent, Subject: "Moved", State: "Idle" });
        Assert.Contains(trace, line => line is { Kind: TraceKind.Played, Outcome: TraceOutcome.Interrupted, State: "Idle" });
        Assert.Contains(trace, line => line is { Kind: TraceKind.Entered, State: "Moved" });
    }

    [Fact]
    public async Task TheTraceSaysWhenNoStateHolds()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("rich", "Rich") { When = When("kamas.atLeast", ("Amount", 1_000_000)), Actions = Chain(Note("r", "rich")) }));

        await UntilAsync(() => bot.Trace.Written > 0);
        run.Stop();
        await run.Completion;

        Assert.Equal(TraceKind.Waiting, bot.Trace.All()[0].Kind);
        Assert.Null(bot.Trace.All()[0].State);
    }

    [Fact]
    public async Task AListOfStepsReportsTheStepItIsAtAndHowManyThereAre()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("only", "Only") { Actions = Chain(Note("a", "a"), Node("w", "wait", ("Seconds", 60)), Note("b", "b")) }));

        await UntilAsync(() => Notes(bot).Count == 1 && run.Status is ActivityStatus.Stepping);

        var progress = run.Activity.Report?.Progress;

        Assert.Equal((1, 3), (progress?.Done, progress?.Total));
        Assert.Equal([2L, 3L], progress?.Text.Values.Select(value => value.Id));

        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(60));

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["a", "b"], Notes(bot));
    }

    [Fact]
    public async Task AListThatRepeatsReportsItsFirstStepAgainWhenItStartsOver()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, Behavior(
            new BehaviorState("loop", "Loop") { Repeat = StepRepeat.Forever, Actions = Chain(Node("w", "wait", ("Seconds", 1)), Node("v", "wait", ("Seconds", 60))) }));

        await UntilAsync(() => run.Status is ActivityStatus.Stepping);
        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(1));
        await UntilAsync(() => run.Activity.Report?.Progress?.Done == 1);

        _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(60));
        await UntilAsync(() => run.Activity.Report?.Progress?.Done == 0);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AStateWithABranchIsNoListAndReportsNoProgress()
    {
        var (connection, bot) = await _behaviors.Play.EnterAsync(300);

        connection.Server(new KamasUpdateEvent { Kamas = 50 });
        await bot.ProcessedAsync();

        var graph = new ActionGraph(
            "if",
            [
                Node("if", NodeCatalog.Branch),
                Node("rich", "kamas.atLeast", ("Amount", 10)),
                Node("yes", "wait", ("Seconds", 60)),
                Note("no", "no"),
            ],
            [
                new GraphLink("rich", GraphPins.Value, "if", "Condition"),
                new GraphLink("if", GraphPins.True, "yes"),
                new GraphLink("if", GraphPins.False, "no"),
            ]);
        var branching = await _behaviors.StartAsync(bot, Behavior(new BehaviorState("only", "Only") { Actions = graph }));

        await UntilAsync(() => branching.Status is ActivityStatus.Stepping);

        Assert.Null(branching.Activity.Report?.Progress);
        branching.Stop();
        await branching.Completion;
    }

    [Fact]
    public async Task AListOfASingleStepReportsNoProgress()
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var single = await _behaviors.StartAsync(bot, Behavior(new BehaviorState("one", "One") { Actions = Chain(Node("w", "wait", ("Seconds", 60))) }));

        await UntilAsync(() => single.Status is ActivityStatus.Stepping);

        Assert.Null(single.Activity.Report?.Progress);
        single.Stop();
        await single.Completion;
    }

    [Theory]
    [InlineData(WatchedGauge.Pods, 90, 899, 1000, false)]
    [InlineData(WatchedGauge.Pods, 90, 900, 1000, true)]
    [InlineData(WatchedGauge.Life, 40, 41, 100, false)]
    [InlineData(WatchedGauge.Life, 40, 40, 100, true)]
    public void AThresholdHoldsThePodsAtOrAboveItAndTheLifeAtOrUnderIt(WatchedGauge gauge, int threshold, int value, int most, bool holds)
    {
        var state = gauge is WatchedGauge.Pods ? new D3B.Bot.Characters.CharacterState { Key = TestPlay.Key, Name = "Character", Weight = value, MaxWeight = most } : new D3B.Bot.Characters.CharacterState { Key = TestPlay.Key, Name = "Character", Life = value, MaxLife = most };

        Assert.Equal(holds, BehaviorActivity.Holds(new ActivityWatch(gauge, threshold, WatchAction.Bank), state));
    }

    [Fact]
    public void AThresholdWithoutItsGaugeIsUnknown()
    {
        Assert.Null(BehaviorActivity.Holds(new ActivityWatch(WatchedGauge.Life, 40, WatchAction.Heal), new D3B.Bot.Characters.CharacterState { Key = TestPlay.Key, Name = "Character" }));
    }
}
