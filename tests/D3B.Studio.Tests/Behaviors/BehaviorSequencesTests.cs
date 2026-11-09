using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Bot.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;
using static D3B.Studio.Tests.Behaviors.TestBehaviors;

namespace D3B.Studio.Tests.Behaviors;

public sealed class BehaviorSequencesTests : IAsyncDisposable
{
    private readonly TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    private static NodeCall Step(string node, params (string Key, JsonNode? Value)[] values)
    {
        return new NodeCall(node, Values(values));
    }

    // The clock moves until the condition holds: a wait registers its timer a moment after the step before it is journaled.
    private async Task AdvanceUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold in time.");
            _behaviors.Play.Time.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    private static BehaviorAsset Empty()
    {
        return new BehaviorAsset { Id = AssetLibrary.NewId(), Name = "List" };
    }

    [Fact]
    public void AListIsWrittenAsOneStateOfChainedActionsAndReadBackInOrder()
    {
        var sequence = new BehaviorSequence([Step("note", ("Text", "a")), Step("wait", ("Seconds", 5)), Step("stop")], Repeats: true);
        var behavior = BehaviorSequences.Write(Empty(), sequence);
        var state = Assert.Single(behavior.States);

        Assert.True(state.Repeats);
        Assert.Equal("step1", state.Actions.Entry);
        Assert.Equal(["step1", "step2", "step3"], state.Actions.Nodes.Select(node => node.Id));
        Assert.Equal([("step1", "step2"), ("step2", "step3")], state.Actions.Links.Select(link => (link.From, link.To)));
        Assert.All(state.Actions.Links, link => Assert.Equal(GraphPins.Next, link.Pin));
        Assert.True(BehaviorSequences.Read(behavior)!.IsSame(sequence));
    }

    [Fact]
    public void AnEmptyListAndABehaviorWithNoStateAreBothAListOfNoStep()
    {
        var written = BehaviorSequences.Write(Empty(), BehaviorSequence.Empty);

        Assert.Equal(BehaviorSequence.Empty, BehaviorSequences.Read(written));
        Assert.Null(Assert.Single(written.States).Actions.Entry);
        Assert.Equal(BehaviorSequence.Empty, BehaviorSequences.Read(Empty()));
    }

    [Fact]
    public void WritingAListKeepsTheBehaviorsIdentityAndDropsWhatAListCannotHold()
    {
        var behavior = Empty() with
        {
            Always = [new BehaviorState("safe", "Safe")],
            Rules = [new BehaviorRule("rule", "Rule", new NodeCall("map.changed", Values()))],
            Variables = [new BehaviorVariable("v", VariableType.Flag, VariableScope.Run)],
            Parameters = [new BehaviorParameter("p", 1)],
        };
        var written = BehaviorSequences.Write(behavior, new BehaviorSequence([Step("stop")], false));

        Assert.Equal((behavior.Id, behavior.Name), (written.Id, written.Name));
        Assert.Empty(written.Always);
        Assert.Empty(written.Rules);
        Assert.Empty(written.Variables);
        Assert.Empty(written.Parameters);
    }

    [Fact]
    public void ABehaviorThatIsMoreThanAListIsNotOne()
    {
        var chain = Chain(Note("a", "a"), Note("b", "b"));

        Assert.Null(BehaviorSequences.Read(Behavior(new BehaviorState("a", "A") { Actions = chain }, new BehaviorState("b", "B") { Actions = chain })));
        Assert.Null(BehaviorSequences.Read(Behavior(new BehaviorState("a", "A") { When = When("flag.on"), Actions = chain })));
        Assert.Null(BehaviorSequences.Read(Behavior(new BehaviorState("a", "A") { IsUrgent = true, Actions = chain })));
        Assert.Null(BehaviorSequences.Read(Behavior(new BehaviorState("a", "A") { States = [new BehaviorState("b", "B")] })));
        Assert.Null(BehaviorSequences.Read(Behavior() with { Always = [new BehaviorState("safe", "Safe")] }));
        Assert.Null(BehaviorSequences.Read(Behavior() with { Parameters = [new BehaviorParameter("p", 1)] }));
        Assert.NotNull(BehaviorSequences.Read(Behavior(new BehaviorState("a", "A") { Actions = chain })));
    }

    [Fact]
    public void ACanvasThatIsNotOneChainIsNotAList()
    {
        var a = Note("a", "a");
        var b = Note("b", "b");
        var c = Note("c", "c");

        BehaviorAsset Of(ActionGraph graph)
        {
            return Behavior(new BehaviorState("s", "S") { Actions = graph });
        }

        // A node off the chain, a fork, a cycle, a data wire, a disabled node and an unknown entry each leave the canvas to 7e.
        Assert.Null(BehaviorSequences.Read(Of(new ActionGraph("a", [a, b], []))));
        Assert.Null(BehaviorSequences.Read(Of(new ActionGraph("a", [a, b, c], [new GraphLink("a", GraphPins.Next, "b"), new GraphLink("a", GraphPins.Next, "c")]))));
        Assert.Null(BehaviorSequences.Read(Of(new ActionGraph("a", [a, b], [new GraphLink("a", GraphPins.Next, "b"), new GraphLink("b", GraphPins.Next, "a")]))));
        Assert.Null(BehaviorSequences.Read(Of(new ActionGraph("a", [a, b], [new GraphLink("a", GraphPins.Value, "b", "Text")]))));
        Assert.Null(BehaviorSequences.Read(Of(new ActionGraph("a", [a with { IsDisabled = true }], []))));
        Assert.Null(BehaviorSequences.Read(Of(new ActionGraph("x", [a], []))));
        Assert.Null(BehaviorSequences.Read(Of(new ActionGraph(null, [a], []))));
    }

    [Fact]
    public void TwoListsAreTheSameWhenTheirStepsHoldTheSameValuesAndTheyRepeatAlike()
    {
        var one = new BehaviorSequence([Step("wait", ("Seconds", 5))], false);

        Assert.True(one.IsSame(new BehaviorSequence([Step("wait", ("Seconds", 5))], false)));
        Assert.False(one.IsSame(new BehaviorSequence([Step("wait", ("Seconds", 6))], false)));
        Assert.False(one.IsSame(new BehaviorSequence([Step("wait", ("Seconds", 5))], true)));
        Assert.False(one.IsSame(new BehaviorSequence([Step("pause", ("Seconds", 5))], false)));
        Assert.False(one.IsSame(new BehaviorSequence([Step("wait", ("Seconds", 5)), Step("stop")], false)));
        Assert.False(one.IsSame(new BehaviorSequence([Step("wait")], false)));
    }

    [Fact]
    public void ABehaviorUsesTheRoutesItFollowsEachOnce()
    {
        var behavior = BehaviorSequences.Write(Empty(), new BehaviorSequence(
        [
            Step(RouteActions.Follow, ("Route", "route-a")),
            Step(RouteActions.Follow, ("Route", "route-b")),
            Step(RouteActions.Follow, ("Route", "route-a")),
            Step(RouteActions.Follow),
            Step("wait", ("Seconds", 5)),
        ], false));

        Assert.Equal(["route-a", "route-b"], behavior.Uses());
        Assert.Equal(["route-a", "route-b"], BehaviorSequences.RoutesOf(behavior));
        Assert.Empty(Behavior().Uses());
    }

    [Fact]
    public async Task AProfileOfAListIsUsedByNoOneButItsBehaviorAndAListUsesItsRoutes()
    {
        await _behaviors.PreparedAsync();

        var route = await _behaviors.Assets.SaveAsync(new RouteAsset { Id = AssetLibrary.NewId(), Name = "Route" }, TestContext.Current.CancellationToken);
        var list = await _behaviors.Assets.SaveAsync(BehaviorSequences.Write(Empty(), new BehaviorSequence([Step(RouteActions.Follow, ("Route", route.Id))], false)), TestContext.Current.CancellationToken);
        var profile = await _behaviors.Assets.SaveAsync(new ProfileAsset { Id = AssetLibrary.NewId(), Name = "Profile", Behavior = list.Id }, TestContext.Current.CancellationToken);

        Assert.Equal([list.Id], _behaviors.Assets.UsedBy(route.Id).Select(asset => asset.Id));
        Assert.Equal([profile.Id], _behaviors.Assets.UsedBy(list.Id).Select(asset => asset.Id));
    }

    [Fact]
    public async Task AListSurvivesItsFileAndIsTheSameWhenReadBack()
    {
        await _behaviors.PreparedAsync();

        var sequence = new BehaviorSequence([Step(RouteActions.Follow, ("Route", "r")), Step("note", ("Text", "hello")), Step("wait", ("Seconds", 3))], Repeats: true);
        var saved = await _behaviors.Assets.SaveAsync(BehaviorSequences.Write(Empty(), sequence), TestContext.Current.CancellationToken);

        await _behaviors.Assets.ReloadAsync(TestContext.Current.CancellationToken);

        var read = _behaviors.Assets.Find<BehaviorAsset>(saved.Id)!;

        Assert.True(BehaviorSequences.Read(read)!.IsSame(sequence));
        Assert.Equal(saved.States[0].Actions.Nodes.Select(node => node.Position), read.States[0].Actions.Nodes.Select(node => node.Position));
    }

    [Fact]
    public async Task EveryNodeTheListOffersIsAnActionWithAnEditableSentence()
    {
        await _behaviors.PreparedAsync();

        foreach (var id in BehaviorSequences.Offered)
        {
            Assert.True(_behaviors.Nodes.TryGet(id, out var node), id);
            Assert.Equal(NodeRole.Action, node.Role);

            foreach (var language in new[] { "en", "fr" })
            {
                var sentence = node.Sentence.In(System.Globalization.CultureInfo.GetCultureInfo(language));

                // Every value is a token of the sentence, or nothing could edit it (D380).
                foreach (var setting in node.Values.All)
                    Assert.Contains("{" + setting.Key + "}", sentence, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task AListOfStepsPlaysInOrderAndIsDoneAtItsEnd()
    {
        await _behaviors.PreparedAsync();

        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = BehaviorSequences.Write(Empty(), new BehaviorSequence([Step("note", ("Text", "first")), Step("wait", ("Seconds", 5)), Step("note", ("Text", "second"))], false));
        var run = await _behaviors.StartAsync(bot, behavior);

        await UntilAsync(() => Notes(bot).Count == 1);
        await AdvanceUntilAsync(() => run.Completion.IsCompleted);

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["first", "second"], Notes(bot));
    }

    [Fact]
    public async Task AListThatRepeatsStartsAgainAtItsEnd()
    {
        await _behaviors.PreparedAsync();

        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = BehaviorSequences.Write(Empty(), new BehaviorSequence([Step("note", ("Text", "lap")), Step("wait", ("Seconds", 1))], true));
        var run = await _behaviors.StartAsync(bot, behavior);

        await UntilAsync(() => Notes(bot).Count == 1);
        await AdvanceUntilAsync(() => Notes(bot).Count >= 2);
        run.Stop();
        await run.Completion;

        Assert.All(Notes(bot), note => Assert.Equal("lap", note));
        Assert.True(Notes(bot).Count >= 2);
    }

    [Fact]
    public async Task AListThatFollowsARouteTheProjectLacksStopsWithoutPlayingTheRest()
    {
        await _behaviors.PreparedAsync();

        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var behavior = BehaviorSequences.Write(Empty(), new BehaviorSequence([Step(RouteActions.Follow, ("Route", "gone")), Step("note", ("Text", "after"))], false));
        var run = await _behaviors.StartAsync(bot, behavior);

        // The route that cannot be followed ends the run, with a note saying so, and what follows it is never played.
        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.DoesNotContain("after", Notes(bot));
        Assert.Contains("gone", string.Join(' ', bot.Journal.All().OfType<D3B.Bot.Journal.NoteEntry>().Select(note => note.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AListWithAStepNoModuleKnowsIsSaidByTheCompiler()
    {
        await _behaviors.PreparedAsync();

        var behavior = BehaviorSequences.Write(Empty(), new BehaviorSequence([Step("nothing.like.it")], false));

        Assert.Contains(BehaviorProblemKind.NodeUnknown, _behaviors.Compile(behavior).Problems.Select(problem => problem.Kind));
    }

    [Fact]
    public async Task ARouteIsASettingOfTheFollowNodeAndNeedsToBeNamed()
    {
        await _behaviors.PreparedAsync();

        Assert.True(_behaviors.Nodes.TryGet(RouteActions.Follow, out var node));

        var setting = Assert.Single(node.Values.All);

        Assert.Equal(SettingKind.Route, setting.Kind);
        Assert.False(setting.IsList);
        Assert.Equal(SettingProblemKind.Required, Assert.Single(node.Values.Check(new RouteFollowValues())).Kind);
        Assert.Empty(node.Values.Check(new RouteFollowValues { Route = "route-a" }));

        var made = RouteActionCalls.Make(node, new RouteFollowValues { Route = "route-a" });

        Assert.Equal("route-a", Assert.IsType<RouteFollowValues>(RouteActionCalls.Read(node, made).Settings).Route);
        Assert.Equal(["Route"], made.Values.Keys);
    }
}
