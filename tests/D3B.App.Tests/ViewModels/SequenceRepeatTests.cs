using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.App.ViewModels;
using D3B.App.Localization;
using D3B.App.ViewModels.Sequences;
using D3B.App.ViewModels.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class SequenceRepeatTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private static NodeCall Step(string node, params (string Key, JsonNode? Value)[] values)
    {
        return new NodeCall(node, values.ToImmutableSortedDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
    }

    private async Task<SequenceEditorViewModel> EditorAsync(StepRepeat list, params NodeCall[] steps)
    {
        await _store.OpenedAsync();

        var page = new SequencesViewModel(_localizer, new Dialogs(), new Toasts());

        page.Refresh(_store.Host.Services);

        var saved = await _store.Assets.SaveAsync(BehaviorSequences.Write(new BehaviorAsset { Id = AssetLibrary.NewId(), Name = "List" }, new BehaviorSequence([.. steps], list)), TestContext.Current.CancellationToken);

        return page.OpenList(saved.Id)!;
    }

    private BehaviorSequence Saved(SequenceEditorViewModel editor)
    {
        return BehaviorSequences.Read(_store.Assets.Find<BehaviorAsset>(editor.Id)!)!;
    }

    [Fact]
    public async Task AStepThatIsPlayedOnceSaysSoAndOffersOnlyWhatAStepCanDo()
    {
        var editor = await EditorAsync(StepRepeat.Once, Step("wait", ("Seconds", 5)));
        var repeat = editor.Steps[0].Repeat!;

        Assert.Equal("once", repeat.Summary);
        Assert.True(repeat.IsOnce);
        Assert.False(repeat.AllowsForever);
        Assert.True(editor.ListRepeat.AllowsForever);
        Assert.Equal(["Pods at least … %", "Life at least … %", "Level at least …", "… at least level …", "At least … kamas", "At least … of …", "In …", "In the area …", "Last fight …", "Running for at least … min"], repeat.Conditions.Select(choice => choice.Name));
    }

    [Fact]
    public async Task AStepIsRepeatedAGivenNumberOfTimesAndTheCountIsSavedWithTheList()
    {
        var editor = await EditorAsync(StepRepeat.Once, Step("wait", ("Seconds", 5)), Step("stop"));
        var repeat = editor.Steps[0].Repeat!;

        repeat.IsTimes = true;

        Assert.Equal((2, "2 times"), (repeat.Count, repeat.Summary));
        Assert.True(editor.IsDirty);

        repeat.Count = 5;

        Assert.Equal("5 times", repeat.Summary);

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Saved(editor);

        Assert.Equal((RepeatKind.Times, 5), (saved.Steps[0].Repeat.Kind, saved.Steps[0].Repeat.Count));
        Assert.Equal(RepeatKind.Once, saved.Steps[1].Repeat.Kind);

        repeat.IsOnce = true;

        Assert.Equal("once", repeat.Summary);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public async Task ACountOutsideItsBoundsIsBroughtBackIn()
    {
        var editor = await EditorAsync(StepRepeat.Times(3), Step("stop"));
        var repeat = editor.Steps[0].Repeat!;

        repeat.IsTimes = true;
        repeat.Count = 0;

        Assert.Equal(2, repeat.Count);

        repeat.Count = 100000;

        Assert.Equal(StepRepeat.MostTimes, repeat.Count);
    }

    [Fact]
    public async Task AnUntilStepOffersTheCatalogsConditionsAndEditsTheOneChosenWhereItStands()
    {
        var editor = await EditorAsync(StepRepeat.Once, Step("wait", ("Seconds", 5)));
        var repeat = editor.Steps[0].Repeat!;

        repeat.IsUntil = true;

        Assert.Equal("pods.atLeast", repeat.Condition!.NodeId);

        repeat.Conditions.Single(choice => choice.Name == "At least … kamas").ChooseCommand.Execute(null);

        var card = repeat.Condition!;

        Assert.Equal("kamas.atLeast", card.NodeId);

        var field = Assert.IsType<NumberFieldViewModel>(card.Editor!.FieldOf("Amount"));

        field.Value = 1000;

        Assert.StartsWith("until at least 1000", repeat.Summary, StringComparison.Ordinal);
        Assert.Contains("1000", repeat.Summary, StringComparison.Ordinal);
        Assert.True(editor.CanSave);

        await editor.SaveCommand.ExecuteAsync(null);

        var until = Saved(editor).Steps[0].Repeat;

        Assert.Equal(RepeatKind.Until, until.Kind);
        Assert.Equal("kamas.atLeast", until.Condition!.Node);
        Assert.Equal(1000, until.Condition.Values["Amount"]!.GetValue<int>());
    }

    [Fact]
    public async Task AnUntilWhoseConditionLacksAValueBlocksTheSave()
    {
        var editor = await EditorAsync(StepRepeat.Once, Step("wait", ("Seconds", 5)));
        var repeat = editor.Steps[0].Repeat!;

        repeat.IsUntil = true;
        repeat.Conditions.Single(choice => choice.Name == "At least … of …").ChooseCommand.Execute(null);

        Assert.True(repeat.HasProblems);
        Assert.False(editor.CanSave);
    }

    [Fact]
    public async Task TheRepeatOfAStepStaysWithItWhenItsValuesChangeOrItMoves()
    {
        var editor = await EditorAsync(StepRepeat.Once, Step("wait", ("Seconds", 5)), Step("stop"));

        editor.Steps[0].Repeat!.IsTimes = true;

        var field = Assert.IsType<NumberFieldViewModel>(editor.Steps[0].Editor!.FieldOf("Seconds"));

        field.Value = 9;
        editor.Steps[0].MoveDownCommand.Execute(null);

        Assert.Equal(["stop", "wait"], editor.Steps.Select(step => step.NodeId));
        Assert.Equal(["once", "2 times"], editor.Steps.Select(step => step.Repeat!.Summary));

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal([RepeatKind.Once, RepeatKind.Times], Saved(editor).Steps.Select(step => step.Repeat.Kind));
    }

    [Fact]
    public async Task UndoGivesTheRepeatBackAndRedoTakesItAgain()
    {
        var editor = await EditorAsync(StepRepeat.Once, Step("wait", ("Seconds", 5)));

        editor.Steps[0].Repeat!.IsTimes = true;
        editor.Undo();

        Assert.Equal("once", editor.Steps[0].Repeat!.Summary);
        Assert.False(editor.IsDirty);

        editor.Redo();

        Assert.Equal("2 times", editor.Steps[0].Repeat!.Summary);
    }

    [Fact]
    public async Task TheListRepeatsForEverNumberedOrUntilAConditionAndTheOldCheckBoxStillMeansForEver()
    {
        var editor = await EditorAsync(StepRepeat.Once, Step("stop"));

        Assert.True(editor.ListRepeat.IsOnce);
        Assert.False(editor.Repeats);

        editor.Repeats = true;

        Assert.True(editor.ListRepeat.IsForever);
        Assert.Equal("for ever", editor.ListRepeat.Summary);

        editor.ListRepeat.IsTimes = true;
        editor.ListRepeat.Count = 4;

        Assert.True(editor.Repeats);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal((RepeatKind.Times, 4), (Saved(editor).Repeat.Kind, Saved(editor).Repeat.Count));
    }

    [Fact]
    public async Task AListThatRepeatsUntilAConditionIsReadBackWithItsCondition()
    {
        var until = StepRepeat.Until(Step("level.atLeast", ("Level", 20)));
        var editor = await EditorAsync(until, Step("stop"));

        Assert.True(editor.ListRepeat.IsUntil);
        Assert.Equal("level.atLeast", editor.ListRepeat.Condition!.NodeId);
        Assert.Contains("20", editor.ListRepeat.Summary, StringComparison.Ordinal);
        Assert.False(editor.IsDirty);
    }
}
