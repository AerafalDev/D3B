using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Settings;
using D3B.Studio.Assets;

namespace D3B.App.Tests.ViewModels;

public sealed class RouteEditorViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private async Task<(RoutesViewModel Routes, RouteEditorViewModel Editor)> OpenAsync(params long[] maps)
    {
        await _store.OpenedAsync();

        var routes = new RoutesViewModel(_localizer, new Dialogs(), new Toasts());

        routes.Use(TestRouteWorld.Services(_store.Assets));

        var route = await _store.Assets.SaveAsync(new RouteAsset { Id = "r1", Name = "North", Anchors = [.. maps.Select(map => new RouteAnchor { MapId = map })] }, TestContext.Current.CancellationToken);
        var editor = routes.OpenRoute(route.Id)!;

        await editor.Planned;

        return (routes, editor);
    }

    [Fact]
    public async Task ARoutesAnchorsShowAsStationsAndTheMapsBetweenAsALegDrawnThroughThem()
    {
        var (_, editor) = await OpenAsync(1, 4);

        Assert.Equal([(1, 0), (4, 0)], editor.Scene.Stations.Select(station => (station.X, station.Y)));

        var leg = Assert.Single(editor.Scene.Legs);

        Assert.Equal([(1, 0), (2, 0), (3, 0), (4, 0)], leg.Squares);
        Assert.Equal(WorldMapLegKind.Path, leg.Kind);
        Assert.Equal(("2 maps", 4, 0), (editor.Steps[0].Gap, editor.MapCount, editor.GapCount));
    }

    [Fact]
    public async Task ATraceAddsAnAnchorOnTheMapPressedOnceAndAPaintOnlyWhereAnExitJoinsTheLastOne()
    {
        var (_, editor) = await OpenAsync();

        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(3, 0, isDrag: true);
        editor.TraceSquare(2, 0, isDrag: true);
        editor.TraceSquare(9, 9, isDrag: false);
        await editor.Planned;

        Assert.Equal([1L, 2L], editor.Steps.Select(step => step.MapId));
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public async Task APaintGoesOnFromTheMapItCrossedLastEvenWhereNoExitJoinedTheOneBefore()
    {
        var (_, editor) = await OpenAsync();

        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(3, 0, isDrag: true);
        editor.TraceSquare(4, 0, isDrag: true);
        await editor.Planned;

        Assert.Equal([1L, 4L], editor.Steps.Select(step => step.MapId));
    }

    [Fact]
    public async Task ChoosingAnotherWorldInTheMenuShowsItsLayerAndTheTypedIdOfItsMapSwitchesToIt()
    {
        var (_, editor) = await OpenAsync(1, 2);

        Assert.Equal([1, 2], editor.Worlds.Select(world => world.Id));
        Assert.Equal(2, editor.Scene.Stations.Count);

        editor.Worlds[1].ChooseCommand.Execute(null);

        Assert.Equal(2, editor.Layer);
        Assert.Empty(editor.Scene.Stations);
        Assert.Equal([false, true], editor.Worlds.Select(world => world.IsChosen));
        Assert.Equal([true, true], editor.Steps.Select(step => step.IsElsewhere));

        editor.Worlds[0].ChooseCommand.Execute(null);
        editor.AddText = "00007";
        editor.AddCommand.Execute(null);

        Assert.Equal(2, editor.Layer);
        Assert.Single(editor.Scene.Stations);
    }

    [Fact]
    public async Task APointOfThePathPinnedBecomesAnAnchorBetweenTheTwoItLayBetween()
    {
        var (_, editor) = await OpenAsync(1, 4);

        editor.PinPathPoint(0, 3, 0);
        await editor.Planned;

        Assert.Equal([1L, 3L, 4L], editor.Steps.Select(step => step.MapId));
        Assert.Equal([false, true, false], editor.Steps.Select(step => step.IsPinned));
        Assert.Equal(1, editor.SelectedIndex);
    }

    [Fact]
    public async Task AnAnchorNoPathReachesIsARedGapSaidWithTheStepItStartsFrom()
    {
        var (_, editor) = await OpenAsync(1, 5);

        var leg = Assert.Single(editor.Scene.Legs);

        Assert.Equal(WorldMapLegKind.Unreachable, leg.Kind);
        Assert.True(editor.Steps[0].IsGapBroken);
        Assert.Equal(("No path between step 1 and step 2.", 0, 1), (editor.Problem, editor.ProblemStep, editor.GapCount));
        Assert.True(editor.HasProblem);
    }

    [Fact]
    public async Task ADraggedStationTakesTheMapOfTheCoordinatesItIsLetGoOn()
    {
        var (_, editor) = await OpenAsync(1, 4);

        editor.MoveStation(1, 3, 0);
        await editor.Planned;

        Assert.Equal([1L, 3L], editor.Steps.Select(step => step.MapId));
        Assert.Equal(("1 map", false), (editor.Steps[0].Gap, editor.Steps[0].IsPinned));
    }

    [Fact]
    public async Task CoordinatesTypedAddTheMapThereAndAMapsIdAddsItWhereverItLies()
    {
        var (_, editor) = await OpenAsync();

        editor.AddText = "3, 0";
        editor.AddCommand.Execute(null);
        editor.AddText = "4";
        editor.AddCommand.Execute(null);

        Assert.True(editor.AddFailed);
        Assert.Equal("4", editor.AddText);

        editor.AddText = "[-8 ; 77]";
        editor.AddCommand.Execute(null);

        Assert.True(editor.AddFailed);

        editor.AddText = "00005";
        editor.AddCommand.Execute(null);
        await editor.Planned;

        Assert.Equal([3L, 5L], editor.Steps.Select(step => step.MapId));
        Assert.Equal("", editor.AddText);
        Assert.False(editor.AddFailed);
    }

    [Fact]
    public async Task CoordinatesSeveralMapsShareAskWhichOneAndTheChoiceAddsIt()
    {
        var (_, editor) = await OpenAsync();

        editor.AddText = "2,0";
        editor.AddCommand.Execute(null);

        var choice = Assert.IsType<SharedChoiceViewModel>(editor.Choice);

        Assert.Equal("2 maps at 2,0", choice.Title);
        Assert.Equal([2L, 9L], choice.Items.Select(item => item.MapId));
        Assert.Equal(["World", "Inside"], choice.Items.Select(item => item.Kind));

        choice.ChooseCommand.Execute(choice.Items[1]);
        await editor.Planned;

        Assert.Null(editor.Choice);
        Assert.Equal([9L], editor.Steps.Select(step => step.MapId));
        Assert.Contains("inside", editor.Steps[0].Place, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStepRemovedOrChosenMovesTheSelection()
    {
        var (_, editor) = await OpenAsync(1, 2, 3);

        editor.Select(1);

        Assert.Equal([false, true, false], editor.Steps.Select(step => step.IsSelected));
        Assert.True(editor.Scene.Stations[1].IsSelected);

        editor.RemoveSelected();
        await editor.Planned;

        Assert.Equal([1L, 3L], editor.Steps.Select(step => step.MapId));
        Assert.Equal(1, editor.SelectedIndex);
    }

    [Fact]
    public async Task ChangingTheCourseOrWhatToDoWithAnUnreachableMapMakesTheRouteDirtyAndSavingKeepsThem()
    {
        var (routes, editor) = await OpenAsync(1, 2, 3);

        Assert.False(editor.IsDirty);

        editor.SetModeCommand.Execute(RouteMode.OneWay);
        editor.SetUnreachableCommand.Execute(RouteUnreachable.Fail);
        editor.TraceSquare(4, 0, isDrag: false);

        Assert.True(editor.IsDirty);
        Assert.True(routes.IsAnyDirty);

        await editor.SaveCommand.ExecuteAsync(null);

        var saved = Assert.IsType<RouteAsset>(_store.Assets.Find("r1"));

        Assert.False(editor.IsDirty);
        Assert.Equal((RouteMode.OneWay, RouteUnreachable.Fail), (saved.Mode, saved.Unreachable));
        Assert.Equal([1L, 2L, 3L, 4L], saved.Anchors.Select(anchor => anchor.MapId));
    }

    [Fact]
    public async Task ALoopOfThreeStepsSaysItsWayBackAndOneWayDoesNot()
    {
        var (_, editor) = await OpenAsync(1, 3, 4);

        Assert.Equal(["1 map", "neighbours", "Back to step 1 · 2 maps"], editor.Steps.Select(step => step.Gap));
        Assert.Equal(3, editor.Scene.Legs.Count);
        Assert.True(editor.Scene.Legs[2].IsClosing);

        editor.SetModeCommand.Execute(RouteMode.OneWay);
        await editor.Planned;

        Assert.Equal(["1 map", "neighbours", ""], editor.Steps.Select(step => step.Gap));
        Assert.Equal(2, editor.Scene.Legs.Count);
    }

    [Fact]
    public async Task ARouteWithChangesAsksBeforeItsTabClosesAndOneWithoutClosesAtOnce()
    {
        var (routes, editor) = await OpenAsync(1, 2);
        var closed = new List<string>();

        routes.Closed = route => closed.Add(route.Id);
        editor.TraceSquare(3, 0, isDrag: false);
        editor.CloseCommand.Execute(null);

        Assert.True(routes.Dialogs.IsOpen);
        Assert.Empty(closed);

        routes.Dialogs.Close();
        await editor.SaveCommand.ExecuteAsync(null);
        editor.CloseCommand.Execute(null);

        Assert.Equal(["r1"], closed);
        Assert.Empty(routes.Open);
    }

    [Fact]
    public async Task UndoTakesBackEachChangeInTurnAndRedoDoesThemAgain()
    {
        var (_, editor) = await OpenAsync(1);

        Assert.False(editor.CanUndo);

        editor.TraceSquare(2, 0, isDrag: false);
        editor.SetModeCommand.Execute(RouteMode.OneWay);
        editor.TraceSquare(3, 0, isDrag: false);

        await editor.Planned;

        Assert.Equal([1L, 2L, 3L], editor.Steps.Select(step => step.MapId));
        Assert.Equal((RouteMode.OneWay, true), (editor.Mode, editor.CanUndo));

        editor.Undo();
        editor.Undo();

        await editor.Planned;

        Assert.Equal([1L, 2L], editor.Steps.Select(step => step.MapId));
        Assert.Equal((RouteMode.Loop, true), (editor.Mode, editor.CanRedo));

        editor.Redo();
        editor.Redo();
        await editor.Planned;

        Assert.Equal([1L, 2L, 3L], editor.Steps.Select(step => step.MapId));
        Assert.Equal((RouteMode.OneWay, false), (editor.Mode, editor.CanRedo));
    }

    [Fact]
    public async Task AChangeAfterAnUndoLeavesNothingToRedoAndUndoingEverythingLeavesTheRouteAsSaved()
    {
        var (_, editor) = await OpenAsync(1, 2);

        editor.TraceSquare(3, 0, isDrag: false);
        editor.Undo();

        Assert.False(editor.IsDirty);

        editor.TraceSquare(4, 0, isDrag: false);

        Assert.False(editor.CanRedo);

        editor.Undo();

        await editor.Planned;

        Assert.Equal([1L, 2L], editor.Steps.Select(step => step.MapId));
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public async Task AGestureIsOneChangeToUndoHoweverManyMapsItCrossed()
    {
        var (_, editor) = await OpenAsync();

        editor.BeginGesture();
        editor.TraceSquare(1, 0, isDrag: false);
        editor.TraceSquare(2, 0, isDrag: true);
        editor.TraceSquare(3, 0, isDrag: true);
        editor.EndGesture();

        // The tests have no synchronization context: the plan of the gesture resumes on a pool thread, and the next trace
        // would rebuild the steps at the same time as it does.
        await editor.Planned;

        editor.TraceSquare(4, 0, isDrag: false);

        await editor.Planned;

        Assert.Equal([1L, 2L, 3L, 4L], editor.Steps.Select(step => step.MapId));

        editor.Undo();

        await editor.Planned;

        Assert.Equal([1L, 2L, 3L], editor.Steps.Select(step => step.MapId));

        editor.Undo();

        await editor.Planned;

        Assert.Empty(editor.Steps);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public async Task TheStepChosenMovesInTheRouteAndTheMovesCanBeUndone()
    {
        var (_, editor) = await OpenAsync(1, 2, 3);

        editor.Select(0);
        // Each change is planned before the next, as on the interface's thread: here no context keeps two plans apart.
        editor.MoveSelected(1);
        await editor.Planned;
        editor.MoveSelected(1);
        await editor.Planned;
        editor.MoveSelected(1);

        await editor.Planned;

        Assert.Equal([2L, 3L, 1L], editor.Steps.Select(step => step.MapId));
        Assert.Equal(2, editor.SelectedIndex);

        editor.Undo();
        await editor.Planned;
        editor.Undo();

        await editor.Planned;

        Assert.Equal([1L, 2L, 3L], editor.Steps.Select(step => step.MapId));
    }

    [Fact]
    public async Task ThePageKeysChooseThePreviousAndNextStepAndStopAtTheEnds()
    {
        var (_, editor) = await OpenAsync(1, 2, 3);

        editor.StepBy(1);
        editor.StepBy(1);
        editor.StepBy(1);
        editor.StepBy(1);

        Assert.Equal(2, editor.SelectedIndex);

        editor.StepBy(-5);

        Assert.Equal(0, editor.SelectedIndex);
    }

    [Fact]
    public async Task AnActionAddedToTheStepChosenIsSaidAsASentenceCountedOnTheStepAndMakesTheRouteDirty()
    {
        var (_, editor) = await OpenAsync(1, 3);

        editor.Select(1);

        Assert.Equal(["Harvest", "Fight", "Take a door", "Forced exit", "Wait", "Take the zaap", "Go to the haven bag", "Talk to an NPC"], editor.OfferedActions.Select(action => action.Name));
        Assert.True(editor.HasNoActions);

        editor.OfferedActions[3].AddCommand.Execute(null);

        var action = Assert.Single(editor.Actions);

        Assert.Equal("Leave by the top, or by cell choose", action.Sentence);
        Assert.Equal((1, "1 action", 1), (editor.Steps[1].ActionCount, editor.Steps[1].ActionsText, editor.ActionTotal));
        Assert.Equal((0, ""), (editor.Steps[0].ActionCount, editor.Steps[0].ActionsText));
        Assert.True(editor.IsDirty);
        Assert.Equal(["Leave by ", "the top", ", or by cell ", "choose"], action.Parts.Select(part => part.Text));
        Assert.Equal([false, true, false, true], action.Parts.Select(part => part.IsToken));
        Assert.Equal([false, false, false, true], action.Parts.Select(part => part.IsDialog));
        Assert.False(editor.HasNoActions);
    }

    [Fact]
    public async Task AValueEditedChangesTheSentenceAndTheRouteWithoutBuildingTheFormsAgain()
    {
        var (_, editor) = await OpenAsync(1, 3);

        editor.Select(0);
        editor.OfferedActions[4].AddCommand.Execute(null);

        var action = editor.Actions[0];
        var field = Assert.IsType<NumberFieldViewModel>(Assert.Single(action.Editor!.Sections.SelectMany(section => section.Fields)));

        Assert.Equal("Wait 5 s", action.Sentence);

        field.Value = 12;

        Assert.Same(action, editor.Actions[0]);
        Assert.Equal("Wait 12 s", action.Sentence);
        Assert.Equal("12", action.Parts.Single(part => part.IsToken).Text);
        Assert.True(editor.CanUndo);

        editor.Undo();

        Assert.Equal("Wait 5 s", editor.Actions[0].Sentence);
    }

    [Fact]
    public async Task ActionsAreRemovedMovedAndTakenBackLikeAnyChangeOfTheRoute()
    {
        var (_, editor) = await OpenAsync(1, 3);

        editor.Select(0);
        editor.OfferedActions[4].AddCommand.Execute(null);
        editor.OfferedActions[5].AddCommand.Execute(null);
        editor.OfferedActions[6].AddCommand.Execute(null);

        Assert.Equal(["Wait 5 s", "Take the zaap", "Go to the haven bag"], editor.Actions.Select(action => action.Sentence));
        Assert.Equal((true, false), (editor.Actions[0].IsFirst, editor.Actions[0].IsLast));

        editor.Actions[2].MoveUpCommand.Execute(null);

        Assert.Equal(["Wait 5 s", "Go to the haven bag", "Take the zaap"], editor.Actions.Select(action => action.Sentence));

        editor.Actions[0].RemoveCommand.Execute(null);

        Assert.Equal(["Go to the haven bag", "Take the zaap"], editor.Actions.Select(action => action.Sentence));
        Assert.Equal("2 actions", editor.Steps[0].ActionsText);

        editor.Undo();

        Assert.Equal(["Wait 5 s", "Go to the haven bag", "Take the zaap"], editor.Actions.Select(action => action.Sentence));
    }

    [Fact]
    public async Task TheActionsAreSavedWithTheRouteAndReadBackAndAnActionNobodyKnowsIsKeptAsItIs()
    {
        var (routes, editor) = await OpenAsync(1, 3);
        var unknown = new D3B.Studio.Behaviors.NodeCall("later.thing", System.Collections.Immutable.ImmutableSortedDictionary<string, System.Text.Json.Nodes.JsonNode?>.Empty.Add("Mood", "calm"));

        var saved = await _store.Assets.SaveAsync((_store.Assets.Find<RouteAsset>("r1")!) with { Anchors = [new RouteAnchor { MapId = 1, Actions = [unknown] }, new RouteAnchor { MapId = 3 }] }, TestContext.Current.CancellationToken);

        editor.Update(saved);
        editor.Select(0);

        var action = Assert.Single(editor.Actions);

        Assert.True(action.IsUnknown);
        Assert.Equal("Unknown action (later.thing)", action.Sentence);

        editor.OfferedActions[4].AddCommand.Execute(null);
        await editor.SaveCommand.ExecuteAsync(null);

        var anchor = routes.Services!.Library.Find<RouteAsset>("r1")!.Anchors[0];

        Assert.Equal(["later.thing", "wait"], anchor.Actions.Select(call => call.Node));
        Assert.Equal("calm", anchor.Actions[0].Values["Mood"]!.GetValue<string>());
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public async Task ThePlusOfAStepOrOfTheActionsOpensTheActionsToChooseFromAndChoosingOneAddsItAndClosesThem()
    {
        var (routes, editor) = await OpenAsync(1, 3);

        editor.Steps[1].AddActionCommand.Execute(null);

        var picker = Assert.IsType<RouteActionPickerViewModel>(routes.Dialogs.Current);

        Assert.Equal(1, editor.SelectedIndex);
        Assert.Equal(["Harvest", "Fight", "Take a door", "Forced exit", "Wait", "Take the zaap", "Go to the haven bag", "Talk to an NPC"], picker.Actions.Select(action => action.Name));
        Assert.All(picker.Actions, action => Assert.NotEmpty(action.Help));

        picker.Actions[4].AddCommand.Execute(null);

        Assert.False(routes.Dialogs.IsOpen);
        Assert.Equal(["Wait 5 s"], editor.Actions.Select(action => action.Sentence));

        editor.ChooseActionCommand.Execute(null);

        Assert.IsType<RouteActionPickerViewModel>(routes.Dialogs.Current);
        Assert.Equal(1, editor.SelectedIndex);

        Assert.IsType<RouteActionPickerViewModel>(routes.Dialogs.Current).CancelCommand.Execute(null);

        Assert.False(routes.Dialogs.IsOpen);
        Assert.Single(editor.Actions);
    }
}
