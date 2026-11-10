using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.Bot.Activities;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using static D3B.Studio.Tests.Behaviors.TestBehaviors;

namespace D3B.Studio.Tests.Behaviors;

public sealed class PlaySequenceTests : IAsyncDisposable
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

    private static NodeCall Note(string text)
    {
        return Step("note", ("Text", text));
    }

    private async Task<BehaviorAsset> ListAsync(string name, StepRepeat repeat, params NodeCall[] steps)
    {
        return await _behaviors.Assets.SaveAsync(BehaviorSequences.Write(new BehaviorAsset { Id = AssetLibrary.NewId(), Name = name }, new BehaviorSequence([.. steps], repeat)), TestContext.Current.CancellationToken);
    }

    private static NodeCall Play(BehaviorAsset list)
    {
        return Step(BehaviorSequences.Play, ("Sequence", list.Id));
    }

    private async Task<(List<string> Notes, ActivityOutcome Outcome)> RunAsync(BehaviorAsset outer)
    {
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, outer);
        var outcome = await run.Completion;

        return ([.. Notes(bot)], outcome);
    }

    [Fact]
    public async Task ASequencePlayedAsAStepPlaysItsStepsInOrderThenTheNextStepOfTheList()
    {
        var inner = await ListAsync("Inner", StepRepeat.Once, Note("a"), Note("b"));
        var outer = await ListAsync("Outer", StepRepeat.Once, Play(inner), Note("after"));
        var (notes, outcome) = await RunAsync(outer);

        Assert.Equal(ActivityOutcome.Done, outcome);
        Assert.Equal(["a", "b", "after"], notes);
    }

    [Fact]
    public async Task ASequencePlayedThreeTimesPlaysItsStepsThreeTimes()
    {
        var inner = await ListAsync("Inner", StepRepeat.Once, Note("a"), Note("b"));
        var outer = await ListAsync("Outer", StepRepeat.Once, Play(inner) with { Repeat = StepRepeat.Times(3) }, Note("after"));
        var (notes, outcome) = await RunAsync(outer);

        Assert.Equal(ActivityOutcome.Done, outcome);
        Assert.Equal(["a", "b", "a", "b", "a", "b", "after"], notes);
    }

    [Fact]
    public async Task ASequencesOwnRepeatIsLeftToTheOneThatPlaysIt()
    {
        var inner = await ListAsync("Inner", StepRepeat.Forever, Note("a"));
        var outer = await ListAsync("Outer", StepRepeat.Once, Play(inner), Note("after"));
        var (notes, outcome) = await RunAsync(outer);

        Assert.Equal(ActivityOutcome.Done, outcome);
        Assert.Equal(["a", "after"], notes);
    }

    [Fact]
    public async Task ASequenceThatHoldsASequenceThatHoldsAnotherPlaysThemAll()
    {
        var deep = await ListAsync("Deep", StepRepeat.Once, Note("deep"));
        var middle = await ListAsync("Middle", StepRepeat.Once, Note("middle"), Play(deep));
        var outer = await ListAsync("Outer", StepRepeat.Once, Play(middle), Note("after"));
        var (notes, _) = await RunAsync(outer);

        Assert.Equal(["middle", "deep", "after"], notes);
    }

    [Fact]
    public async Task ASequenceThatPlaysItselfEndsTheRunWithANoteInsteadOfNeverEnding()
    {
        var list = await ListAsync("Itself", StepRepeat.Once, Note("a"));
        var itself = await _behaviors.Assets.SaveAsync(BehaviorSequences.Write(list, new BehaviorSequence([Note("a"), Play(list)], StepRepeat.Once)), TestContext.Current.CancellationToken);
        var (notes, outcome) = await RunAsync(itself);

        Assert.Equal(ActivityOutcome.Done, outcome);
        Assert.Equal(3, notes.Count);
        Assert.Equal(["a", "a"], notes.Take(2));
        Assert.Contains("Itself", notes[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASequenceThatIsNoLongerInTheProjectEndsTheRunWithANote()
    {
        var outer = await ListAsync("Outer", StepRepeat.Once, Step(BehaviorSequences.Play, ("Sequence", "gone")), Note("after"));
        var (notes, outcome) = await RunAsync(outer);

        Assert.Equal(ActivityOutcome.Done, outcome);
        Assert.Single(notes);
        Assert.Contains("gone", notes[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStopInASequenceEndsTheRunAndTheStepsAfterItNeverPlay()
    {
        var inner = await ListAsync("Inner", StepRepeat.Once, Note("a"), Step("stop"), Note("never"));
        var outer = await ListAsync("Outer", StepRepeat.Once, Play(inner), Note("after"));
        var (notes, outcome) = await RunAsync(outer);

        Assert.Equal(ActivityOutcome.Done, outcome);
        Assert.Equal(["a"], notes);
    }

    [Fact]
    public async Task APauseInASequenceGoesOnFromTheStepAfterItOnceResumedBeforeTheNextStepOfTheList()
    {
        var inner = await ListAsync("Inner", StepRepeat.Once, Note("a"), Step("pause"), Note("b"));
        var outer = await ListAsync("Outer", StepRepeat.Once, Play(inner), Note("after"));
        var (_, bot) = await _behaviors.Play.EnterAsync(300);
        var run = await _behaviors.StartAsync(bot, outer);

        await UntilAsync(() => bot.IsPaused && run.Status is ActivityStatus.Waiting);
        Assert.Equal(["a"], Notes(bot));

        bot.Resume();

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(["a", "b", "after"], Notes(bot));
    }

    [Fact]
    public void ASequenceKeepsItsPlaceForARunThatIsCutAndStartsOverWhenTheRunBeginsAgain()
    {
        var runs = new SequenceRuns();
        var started = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        var made = 0;
        var key = new D3B.Bot.Characters.CharacterKey(1, 2);

        StateVisit Make()
        {
            made++;

            return new StateVisit(new CompiledState { Id = "s", Name = "S" }, _behaviors.Services);
        }

        var first = runs.Of(key, "list", started, Make);

        Assert.Same(first, runs.Of(key, "list", started, Make));
        Assert.Equal(1, made);
        Assert.NotSame(first, runs.Of(key, "list", started.AddMinutes(1), Make));
        Assert.Equal(2, made);

        runs.Forget(key, "list");

        runs.Of(key, "list", started, Make);

        Assert.Equal(3, made);
    }

    [Fact]
    public void AListUsesTheSequencesItPlaysAndReachesTheOnesThoseUse()
    {
        var deep = new BehaviorAsset { Id = "deep", Name = "Deep" };
        var middle = BehaviorSequences.Write(new BehaviorAsset { Id = "middle", Name = "Middle" }, new BehaviorSequence([Play(deep)], StepRepeat.Once));
        var outer = BehaviorSequences.Write(new BehaviorAsset { Id = "outer", Name = "Outer" }, new BehaviorSequence([Step(BehaviorSequences.Play, ("Sequence", "middle")), Step(BehaviorSequences.Play, ("Sequence", "middle"))], StepRepeat.Once));
        var all = new Dictionary<string, BehaviorAsset> { [deep.Id] = deep, [middle.Id] = middle, [outer.Id] = outer }.ToImmutableDictionary();

        Assert.Equal(["middle"], outer.Uses());
        Assert.Equal(["deep"], middle.Uses());
        Assert.True(BehaviorSequences.Reaches(outer, "deep", id => all.GetValueOrDefault(id)));
        Assert.False(BehaviorSequences.Reaches(deep, "outer", id => all.GetValueOrDefault(id)));
        Assert.False(BehaviorSequences.Reaches(outer, "outer", id => all.GetValueOrDefault(id)));

        var itself = BehaviorSequences.Write(new BehaviorAsset { Id = "itself", Name = "Itself" }, new BehaviorSequence([Step(BehaviorSequences.Play, ("Sequence", "itself"))], StepRepeat.Once));

        Assert.True(BehaviorSequences.Reaches(itself, "itself", id => id == "itself" ? itself : null));
    }
}
