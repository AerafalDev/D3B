using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Sequences;
using D3B.App.ViewModels.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class SequencesViewModelTests : IAsyncDisposable
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

    private async Task<SequencesViewModel> PageAsync()
    {
        await _store.OpenedAsync();

        var page = new SequencesViewModel(_localizer, new Dialogs(), new Toasts());

        page.Refresh(_store.Host.Services);

        return page;
    }

    private async Task<BehaviorAsset> ListAsync(string name, params NodeCall[] steps)
    {
        return await _store.Assets.SaveAsync(BehaviorSequences.Write(new BehaviorAsset { Id = AssetLibrary.NewId(), Name = name }, new BehaviorSequence([.. steps], false)), TestContext.Current.CancellationToken);
    }

    private static void Add(SequencesViewModel page, SequenceEditorViewModel editor, string name)
    {
        editor.ChooseStepCommand.Execute(null);

        var picker = Assert.IsType<RouteActionPickerViewModel>(page.Dialogs.Current);

        picker.Actions.Single(action => action.Name == name).AddCommand.Execute(null);
        Assert.Null(page.Dialogs.Current);
    }

    [Fact]
    public async Task ANewListIsAnEmptyFileInTheFolderChosenNamedFreshAndOpenedInItsTab()
    {
        var page = await PageAsync();
        var opened = new List<string>();

        page.Opened = editor => opened.Add(editor.Name);
        await _store.Assets.CreateFolderAsync("Farm", TestContext.Current.CancellationToken);

        var first = await page.CreateAsync("Farm");
        var second = await page.CreateAsync("");

        Assert.Equal(["New list of steps", "New list of steps 2"], opened);
        Assert.Equal("Farm", Path.GetDirectoryName(_store.Assets.Find(first!.Id)!.Path));
        Assert.Equal(AssetKind.Behavior, _store.Assets.Find(second!.Id)!.Kind);
        Assert.Equal([first, second], page.Open);
        Assert.True(first!.IsEmpty);
        Assert.False(first.IsDirty);
        Assert.Equal(BehaviorSequence.Empty, BehaviorSequences.Read(_store.Assets.Find<BehaviorAsset>(first.Id)!));
    }

    [Fact]
    public async Task OpeningAListTwiceKeepsOneEditorAndABehaviorThatIsNotAListOrIsGivenOpensNoneAndSaysSo()
    {
        var page = await PageAsync();
        var list = await ListAsync("North");
        var canvas = await _store.Assets.SaveAsync(new BehaviorAsset { Id = AssetLibrary.NewId(), Name = "Canvas", Always = [new BehaviorState("safe", "Safe")] }, TestContext.Current.CancellationToken);
        var given = _store.Assets.OfType<BehaviorAsset>().First(behavior => behavior.IsGiven);

        Assert.Same(page.OpenList(list.Id), page.OpenList(list.Id));
        Assert.Single(page.Open);
        Assert.True(page.IsList(list.Id));
        Assert.False(page.IsList(canvas.Id));
        Assert.False(page.IsList(given.Id));
        Assert.Null(page.OpenList(canvas.Id));
        Assert.Null(page.OpenList(given.Id));
        Assert.Null(page.OpenList("nobody"));
        Assert.Equal(2, page.Toasts.Recent.Count(toast => toast.Text.Contains("canvas", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AStepIsAddedFromTheMenuAndAFollowedRouteIsPickedAmongTheProjectsNeverTyped()
    {
        var page = await PageAsync();
        var ct = TestContext.Current.CancellationToken;
        var south = await _store.Assets.SaveAsync(new RouteAsset { Id = "r-south", Name = "South", Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }] }, ct);

        await _store.Assets.SaveAsync(new RouteAsset { Id = "r-north", Name = "North", Anchors = [new RouteAnchor { MapId = 3 }] }, ct);

        var editor = (await page.CreateAsync(""))!;

        Assert.Equal(["Follow a route", "Harvest", "Fight", "Go to the haven bag", "Wait", "Write a note", "Pause", "Stop"], editor.OfferedSteps.Select(step => step.Name));

        Add(page, editor, "Follow a route");

        var follow = Assert.Single(editor.Steps);

        Assert.True(editor.IsDirty);
        Assert.False(editor.CanSave);
        Assert.True(follow.HasProblems);
        Assert.Equal("Follow the route choose a route", follow.Sentence);

        var field = Assert.IsType<RouteFieldViewModel>(follow.Editor!.FieldOf("Route"));

        field.PickCommand.Execute(null);

        var picker = Assert.IsType<RoutePickerViewModel>(page.Dialogs.Current);

        Assert.Equal(["North", "South"], picker.Routes.Select(route => route.Name));
        Assert.Equal(["1 map", "2 maps"], picker.Routes.Select(route => route.Detail));
        Assert.False(picker.CanClear);
        picker.Routes[1].ChooseCommand.Execute(null);

        Assert.Null(page.Dialogs.Current);
        Assert.Equal("Follow the route South", follow.Sentence);
        Assert.False(follow.HasProblems);
        Assert.True(editor.CanSave);

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = BehaviorSequences.Read(_store.Assets.Find<BehaviorAsset>(editor.Id)!)!;

        Assert.False(editor.IsDirty);
        Assert.Equal([RouteActions.Follow], saved.Steps.Select(step => step.Node));
        Assert.Equal(south.Id, saved.Steps[0].Values["Route"]!.GetValue<string>());
        Assert.Contains(page.Toasts.Recent, toast => toast.Text.Contains("saved", StringComparison.Ordinal));
        Assert.Equal([editor.Id], _store.Assets.UsedBy(south.Id).Select(asset => asset.Id));

        field.PickCommand.Execute(null);
        Assert.True(Assert.IsType<RoutePickerViewModel>(page.Dialogs.Current).CanClear);
        Assert.Equal(2, ((RoutePickerViewModel)page.Dialogs.Current).Routes.Count(route => route.Name is "North" or "South"));
        ((RoutePickerViewModel)page.Dialogs.Current).ClearCommand.Execute(null);

        Assert.True(follow.HasProblems);
        Assert.False(editor.CanSave);
    }

    [Fact]
    public async Task AValueTypedInAStepsTokenChangesTheListWithoutBuildingItsFormsAgain()
    {
        var page = await PageAsync();
        var list = await ListAsync("Wait", Step("wait", ("Seconds", 5)));
        var editor = page.OpenList(list.Id)!;
        var step = editor.Steps[0];

        Assert.Equal("Wait 5 s", step.Sentence);
        Assert.False(editor.IsDirty);

        var field = Assert.IsType<NumberFieldViewModel>(step.Editor!.FieldOf("Seconds"));

        field.Value = 9;

        Assert.Same(step, editor.Steps[0]);
        Assert.Equal("Wait 9 s", step.Sentence);
        Assert.True(editor.IsDirty);
        Assert.True(editor.CanSave);

        field.Value = 5;

        Assert.False(editor.IsDirty);
        Assert.False(editor.CanSave);
    }

    [Fact]
    public async Task StepsAreMovedAndRemovedAndRepeatingIsSavedWithTheList()
    {
        var page = await PageAsync();
        var list = await ListAsync("Order", Step("note", ("Text", "a")), Step("wait", ("Seconds", 5)), Step("stop"));
        var editor = page.OpenList(list.Id)!;

        Assert.Equal([1, 2, 3], editor.Steps.Select(step => step.Number));
        Assert.True(editor.Steps[0].IsFirst);
        Assert.True(editor.Steps[2].IsLast);

        editor.Steps[2].MoveUpCommand.Execute(null);
        editor.Steps[0].RemoveCommand.Execute(null);
        editor.Repeats = true;

        Assert.Equal(["stop", "wait"], editor.Steps.Select(step => step.NodeId));
        Assert.True(editor.IsDirty);

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = BehaviorSequences.Read(_store.Assets.Find<BehaviorAsset>(list.Id)!)!;

        Assert.Equal(["stop", "wait"], saved.Steps.Select(step => step.Node));
        Assert.True(saved.Repeats);

        editor.Repeats = false;
        editor.Repeats = true;

        Assert.False(editor.IsDirty);
    }

    [Fact]
    public async Task AStepDraggedTakesItsNewPlaceAndADropWhereItIsOrOutsideTheListChangesNothing()
    {
        var page = await PageAsync();
        var list = await ListAsync("Order", Step("note", ("Text", "a")), Step("wait", ("Seconds", 5)), Step("stop"));
        var editor = page.OpenList(list.Id)!;

        editor.ReorderCommand.Execute(new ReorderRequest(0, 2));

        Assert.Equal(["wait", "stop", "note"], editor.Steps.Select(step => step.NodeId));
        Assert.True(editor.IsDirty);

        editor.ReorderCommand.Execute(new ReorderRequest(1, 1));
        editor.ReorderCommand.Execute(new ReorderRequest(1, 7));

        Assert.Equal(["wait", "stop", "note"], editor.Steps.Select(step => step.NodeId));

        editor.ReorderCommand.Execute(new ReorderRequest(2, 0));

        Assert.Equal(["note", "wait", "stop"], editor.Steps.Select(step => step.NodeId));
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public async Task UndoTakesBackEachChangeOfTheListAndRedoDoesItAgainAndUndoingToTheSavedListIsNotDirty()
    {
        var page = await PageAsync();
        var list = await ListAsync("Order", Step("note", ("Text", "a")), Step("wait", ("Seconds", 5)));
        var editor = page.OpenList(list.Id)!;

        Assert.False(editor.CanUndo);

        editor.Steps[1].MoveUpCommand.Execute(null);
        editor.Steps[0].RemoveCommand.Execute(null);
        editor.Repeats = true;

        Assert.Equal((true, false), (editor.CanUndo, editor.CanRedo));

        editor.Undo();

        Assert.False(editor.Repeats);
        Assert.Equal(["note"], editor.Steps.Select(step => step.NodeId));
        Assert.True(editor.CanRedo);

        editor.Undo();

        Assert.Equal(["wait", "note"], editor.Steps.Select(step => step.NodeId));

        editor.Undo();

        Assert.Equal(["note", "wait"], editor.Steps.Select(step => step.NodeId));
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);

        editor.Redo();
        editor.Redo();
        editor.Redo();

        Assert.Equal(["note"], editor.Steps.Select(step => step.NodeId));
        Assert.True(editor.Repeats);
        Assert.True(editor.IsDirty);
        Assert.False(editor.CanRedo);

        // A change after an undo leaves nothing to do again.
        editor.Undo();
        editor.Repeats = false;
        editor.Steps[0].RemoveCommand.Execute(null);

        Assert.False(editor.CanRedo);
    }

    [Fact]
    public async Task ANumberTypedInAStepIsOneChangeToTakeBackHoweverManyKeysItTookAndAnotherFieldIsAnother()
    {
        var page = await PageAsync();
        var list = await ListAsync("Waits", Step("wait", ("Seconds", 5)), Step("wait", ("Seconds", 6)));
        var editor = page.OpenList(list.Id)!;

        var first = Assert.IsType<NumberFieldViewModel>(editor.Steps[0].Editor!.FieldOf("Seconds"));
        var second = Assert.IsType<NumberFieldViewModel>(editor.Steps[1].Editor!.FieldOf("Seconds"));

        first.Value = 1;
        first.Value = 12;
        first.Value = 123;
        second.Value = 9;

        editor.Undo();

        Assert.Equal(["Wait 123 s", "Wait 6 s"], editor.Steps.Select(step => step.Sentence));

        editor.Undo();

        Assert.Equal(["Wait 5 s", "Wait 6 s"], editor.Steps.Select(step => step.Sentence));
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public async Task ALoopFollowedByAnotherStepIsWarnedOfBecauseItNeverEnds()
    {
        var page = await PageAsync();
        var ct = TestContext.Current.CancellationToken;
        var loop = await _store.Assets.SaveAsync(new RouteAsset { Id = "r-loop", Name = "Laps", Mode = RouteMode.Loop }, ct);
        var once = await _store.Assets.SaveAsync(new RouteAsset { Id = "r-once", Name = "Once", Mode = RouteMode.OneWay }, ct);
        var list = await ListAsync("Laps then wait", Step(RouteActions.Follow, ("Route", loop.Id)), Step("wait", ("Seconds", 5)));
        var onlyLoop = await ListAsync("Laps", Step(RouteActions.Follow, ("Route", loop.Id)));
        var oneWay = await ListAsync("Once then wait", Step(RouteActions.Follow, ("Route", once.Id)), Step("wait", ("Seconds", 5)));

        Assert.Equal("The route “Laps” is a loop: it never ends, so the steps after step 1 never play.", page.OpenList(list.Id)!.Warning);
        Assert.Null(page.OpenList(onlyLoop.Id)!.Warning);
        Assert.Null(page.OpenList(oneWay.Id)!.Warning);
    }

    [Fact]
    public async Task AListDeletedFromTheProjectClosesItsEditorAndOneChangedTakesTheFileWhenNothingIsUnsaved()
    {
        var page = await PageAsync();
        var ct = TestContext.Current.CancellationToken;
        var list = await ListAsync("Order", Step("note", ("Text", "a")));
        var editor = page.OpenList(list.Id)!;
        var closed = new List<string>();

        page.Closed = item => closed.Add(item.Name);
        await _store.Assets.SaveAsync(BehaviorSequences.Write(list, new BehaviorSequence([Step("note", ("Text", "a")), Step("stop")], true)), ct);
        page.Refresh(null);

        Assert.Equal(["note", "stop"], editor.Steps.Select(step => step.NodeId));
        Assert.True(editor.Repeats);
        Assert.False(editor.IsDirty);

        editor.Steps[0].RemoveCommand.Execute(null);
        await _store.Assets.SaveAsync(BehaviorSequences.Write(list, new BehaviorSequence([Step("pause")], false)), ct);
        page.Refresh(null);

        // Its own changes are kept over the file's.
        Assert.Equal(["stop"], editor.Steps.Select(step => step.NodeId));

        await _store.Assets.DeleteAsync(list.Id, ct);
        page.Refresh(null);

        Assert.Equal(["Order"], closed);
        Assert.Empty(page.Open);
        Assert.Null(page.Find(list.Id));
    }

    [Fact]
    public async Task ClosingAListWithChangesAsksFirstAndSaveAllSavesEveryListEdited()
    {
        var page = await PageAsync();
        var first = page.OpenList((await ListAsync("One", Step("stop"))).Id)!;
        var second = page.OpenList((await ListAsync("Two", Step("pause"))).Id)!;

        first.Steps[0].RemoveCommand.Execute(null);
        second.Repeats = true;

        Assert.True(page.IsAnyDirty);

        first.CloseCommand.Execute(null);

        Assert.IsType<QuestionViewModel>(page.Dialogs.Current);
        Assert.Contains(first, page.Open);

        page.Dialogs.Close();
        page.SaveAll();
        await Waiting.UntilAsync(() => !page.IsAnyDirty, TimeSpan.FromSeconds(10));

        Assert.Empty(BehaviorSequences.Read(_store.Assets.Find<BehaviorAsset>(first.Id)!)!.Steps);
        Assert.True(BehaviorSequences.Read(_store.Assets.Find<BehaviorAsset>(second.Id)!)!.Repeats);

        first.CloseCommand.Execute(null);

        Assert.DoesNotContain(first, page.Open);
    }

    [Fact]
    public async Task TheStepsAreSaidAgainInTheLanguageChosen()
    {
        var page = await PageAsync();
        var editor = page.OpenList((await ListAsync("Order", Step("wait", ("Seconds", 5)))).Id)!;

        _localizer.Use(Localizer.French);
        page.Retell();

        Assert.Equal("Suivre un trajet", editor.OfferedSteps[0].Name);
        Assert.StartsWith("Attendre", editor.Steps[0].Sentence, StringComparison.Ordinal);
    }
}
