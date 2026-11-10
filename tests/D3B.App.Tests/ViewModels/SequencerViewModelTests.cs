using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Avalonia.Input;
using D3B.App.Controls.Sequencer;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Settings;
using D3B.App.ViewModels.World;
using D3B.Bot.Characters;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class SequencerViewModelTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private static NodeCall Call(string node, object values)
    {
        var catalog = TestRouteWorld.Nodes();

        Assert.True(catalog.TryGet(node, out var definition));

        return RouteActionCalls.Make(definition, values);
    }

    private static RouteAnchor Step(long map, params NodeCall[] actions)
    {
        return new RouteAnchor { MapId = map, Actions = [.. actions] };
    }

    private async Task<(RoutesViewModel Routes, RouteEditorViewModel Editor)> OpenAsync(params RouteAnchor[] steps)
    {
        await _store.OpenedAsync();

        var routes = new RoutesViewModel(_localizer, new Dialogs(), new Toasts());

        routes.Use(TestRouteWorld.Services(_store.Assets));

        var route = await _store.Assets.SaveAsync(new RouteAsset { Id = "r1", Name = "North", Anchors = [.. steps] }, TestContext.Current.CancellationToken);
        var editor = routes.OpenRoute(route.Id)!;

        await editor.Planned;

        return (routes, editor);
    }

    [Fact]
    public async Task ARoutesActionsAreDrawnAsClipsOnATrackForEachKindInTheOrderOfTheMenu()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var fight = Call(RouteActions.Fight, new RouteFightValues());
        var door = Call(RouteActions.Door, new RouteDoorValues { Cell = 312 });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, fight, ash), Step(3, ash), Step(4, door));
        var scene = editor.Sequencer.Scene;

        Assert.Equal([RouteActions.Harvest, RouteActions.Fight, RouteActions.Door], scene.Tracks.Select(track => track.Node));
        Assert.Equal(["Harvest", "Fight", "Take a door"], scene.Tracks.Select(track => track.Name));
        Assert.Equal([(0, 2)], scene.Tracks[0].Clips.Select(clip => (clip.First, clip.Last)));
        Assert.Equal([(1, 1)], scene.Tracks[1].Clips.Select(clip => (clip.First, clip.Last)));
        Assert.Equal([(3, 3)], scene.Tracks[2].Clips.Select(clip => (clip.First, clip.Last)));
        Assert.Contains("Harvest", scene.Tracks[0].Clips[0].Text, StringComparison.Ordinal);
        Assert.Contains("312", scene.Tracks[2].Clips[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRulerHoldsAStepForEachAnchorWithItsNumberItsCoordinatesAndItsZone()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2), Step(3));
        var scene = editor.Sequencer.Scene;

        Assert.Equal(["1", "2", "3"], scene.Steps.Select(step => step.Number));
        Assert.Equal(["1,0", "2,0", "3,0"], scene.Steps.Select(step => step.Place));
        Assert.All(scene.Steps, step => Assert.NotNull(step.Zone));
        Assert.Empty(scene.Tracks);
        Assert.True(editor.Sequencer.HasSteps);
    }

    [Fact]
    public async Task ClipsOfOneKindThatOverlapStandInLanesOfTheirTrack()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var oak = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [2] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash, oak), Step(3, oak));
        var track = Assert.Single(editor.Sequencer.Scene.Tracks);

        Assert.Equal(2, track.Lanes);
        Assert.Equal([(0, 1, 0), (1, 2, 1)], track.Clips.Select(clip => (clip.First, clip.Last, clip.Lane)));
    }

    [Fact]
    public async Task TheStepsSelectedAreTheEditorsAndAPressOnTheRulerChoosesThemWithControlAndShift()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2), Step(3), Step(4));
        var sequencer = editor.Sequencer;

        sequencer.PressStep(1, KeyModifiers.None);
        sequencer.PressStep(3, KeyModifiers.Control);

        Assert.Equal([1, 3], sequencer.SelectedSteps);
        Assert.Equal([1, 3], editor.SelectedSteps);

        sequencer.PressStep(0, KeyModifiers.Shift);

        Assert.Equal([0, 1, 2, 3], sequencer.SelectedSteps);

        editor.Select(2);

        Assert.Equal([2], sequencer.SelectedSteps);
    }

    [Fact]
    public async Task APressOnAClipSelectsItAndLeavesTheStepsAndAPressOnTheRulerLetsItGo()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash), Step(3));
        var sequencer = editor.Sequencer;

        editor.Select(2);

        Assert.Equal(-1, sequencer.SelectedClip);

        var clip = sequencer.Scene.Tracks[0].Clips[0];

        sequencer.PressClip(clip.Id);

        Assert.Equal(clip.Id, sequencer.SelectedClip);
        Assert.Equal([2], sequencer.SelectedSteps);

        sequencer.PressStep(0, KeyModifiers.None);

        Assert.Equal(-1, sequencer.SelectedClip);
    }

    [Fact]
    public async Task ATrackAddedFromThePaletteShowsEmptyAndLeavesThePalette()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2));
        var sequencer = editor.Sequencer;

        Assert.DoesNotContain(RouteActions.Harvest, sequencer.Palette.Select(item => item.Node));
        Assert.Contains(RouteActions.Fight, sequencer.Palette.Select(item => item.Node));

        sequencer.AddTrack(RouteActions.Fight);

        Assert.Equal([RouteActions.Harvest, RouteActions.Fight], sequencer.Scene.Tracks.Select(track => track.Node));
        Assert.Empty(sequencer.Scene.Tracks[1].Clips);
        Assert.Equal(1, sequencer.Scene.Tracks[1].Lanes);
        Assert.DoesNotContain(RouteActions.Fight, sequencer.Palette.Select(item => item.Node));
    }

    [Fact]
    public async Task TheSceneIsMadeAgainWhenTheRouteChanges()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1), Step(2));

        Assert.Empty(editor.Sequencer.Scene.Tracks);

        editor.Update(new RouteAsset { Id = "r1", Name = "North", Anchors = [Step(1, ash), Step(2, ash), Step(3)] });

        Assert.Equal(3, editor.Sequencer.Scene.Steps.Count);
        Assert.Equal([(0, 1)], Assert.Single(editor.Sequencer.Scene.Tracks).Clips.Select(clip => (clip.First, clip.Last)));
    }

    [Fact]
    public async Task AnActionTheHostDoesNotKnowIsATrackNamedByItsNode()
    {
        var unknown = new NodeCall("mod.sing", ImmutableSortedDictionary<string, JsonNode?>.Empty);
        var (_, editor) = await OpenAsync(Step(1, unknown), Step(2, unknown));
        var track = Assert.Single(editor.Sequencer.Scene.Tracks);

        Assert.Equal(("mod.sing", "mod.sing"), (track.Node, track.Name));
        Assert.Equal(WorldMapActionKind.Other, track.Kind);
    }

    private static string[] Nodes(RouteEditorViewModel editor, int step)
    {
        return [.. editor.CurrentAnchors[step].Actions.Select(action => action.Node)];
    }

    [Fact]
    public async Task APressAndADragOnATracksEmptyCellsDrawsAClipOfTheKindOverTheStepsCrossedAndSelectsIt()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2), Step(3), Step(4));
        var sequencer = editor.Sequencer;

        sequencer.AddTrack(RouteActions.Fight);
        sequencer.BeginDraw(0, 1);
        sequencer.Drag(3);
        sequencer.Drag(2);
        sequencer.EndGesture();

        Assert.Equal([[], [RouteActions.Fight], [RouteActions.Fight], []], Enumerable.Range(0, 4).Select(step => Nodes(editor, step)));

        var clip = Assert.Single(Assert.Single(sequencer.Scene.Tracks).Clips);

        Assert.Equal((1, 2), (clip.First, clip.Last));
        Assert.Equal(clip.Id, sequencer.SelectedClip);
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public async Task APressOnAnEmptyCellWithNoDragDrawsAClipOfOneStep()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2));

        editor.Sequencer.AddTrack(RouteActions.Door);
        editor.Sequencer.BeginDraw(0, 1);
        editor.Sequencer.EndGesture();

        Assert.Equal([RouteActions.Door], Nodes(editor, 1));
        Assert.Empty(Nodes(editor, 0));
    }

    [Fact]
    public async Task AGestureIsOneChangeToUndoWhateverTheStepsItCrossed()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2), Step(3), Step(4));
        var sequencer = editor.Sequencer;

        sequencer.AddTrack(RouteActions.Fight);
        sequencer.BeginDraw(0, 0);
        sequencer.Drag(1);
        sequencer.Drag(2);
        sequencer.Drag(3);
        sequencer.EndGesture();
        editor.Undo();

        Assert.All(editor.CurrentAnchors, anchor => Assert.Empty(anchor.Actions));
        Assert.False(editor.CanUndo);
        Assert.Empty(sequencer.Scene.Tracks.SelectMany(track => track.Clips));
    }

    [Fact]
    public async Task ADrawnClipThatNeedsAValueSaysSoOnTheClip()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2));

        editor.Sequencer.AddTrack(RouteActions.Harvest);
        editor.Sequencer.BeginDraw(0, 0);
        editor.Sequencer.EndGesture();

        Assert.True(Assert.Single(Assert.Single(editor.Sequencer.Scene.Tracks).Clips).HasProblems);
    }

    [Fact]
    public async Task DraggingAClipsRightEdgeAndItsLeftEdgeChangesTheStepsItCovers()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash), Step(3), Step(4));
        var sequencer = editor.Sequencer;
        var clip = sequencer.Scene.Tracks[0].Clips[0];

        sequencer.BeginClip(clip.Id, SequencerEdge.Right, 1, KeyModifiers.None);
        sequencer.Drag(3);
        sequencer.EndGesture();

        Assert.Equal((0, 3), (sequencer.Scene.Tracks[0].Clips[0].First, sequencer.Scene.Tracks[0].Clips[0].Last));

        sequencer.BeginClip(sequencer.SelectedClip, SequencerEdge.Left, 0, KeyModifiers.None);
        sequencer.Drag(2);
        sequencer.EndGesture();

        var now = Assert.Single(sequencer.Scene.Tracks[0].Clips);

        Assert.Equal((2, 3), (now.First, now.Last));
        Assert.Equal(now.Id, sequencer.SelectedClip);
        Assert.Empty(Nodes(editor, 0));
        Assert.Empty(Nodes(editor, 1));
    }

    [Fact]
    public async Task DraggingAClipsBodyMovesItWithoutReorderingTheOtherActionsAndShiftCopiesIt()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var fight = Call(RouteActions.Fight, new RouteFightValues());
        var (_, editor) = await OpenAsync(Step(1, ash, fight), Step(2, ash), Step(3, fight), Step(4));
        var sequencer = editor.Sequencer;
        var clip = sequencer.Scene.Tracks[0].Clips[0];

        sequencer.BeginClip(clip.Id, SequencerEdge.None, 0, KeyModifiers.None);
        sequencer.Drag(2);
        sequencer.EndGesture();

        Assert.Equal([RouteActions.Fight], Nodes(editor, 0));
        Assert.Empty(Nodes(editor, 1));
        Assert.Equal([RouteActions.Fight, RouteActions.Harvest], Nodes(editor, 2));
        Assert.Equal([RouteActions.Harvest], Nodes(editor, 3));

        var moved = sequencer.Scene.Tracks[0].Clips[0];

        sequencer.BeginClip(moved.Id, SequencerEdge.None, 2, KeyModifiers.Shift);
        sequencer.Drag(1);
        sequencer.EndGesture();

        Assert.Equal([RouteActions.Fight], Nodes(editor, 0));
        Assert.Equal([RouteActions.Harvest], Nodes(editor, 1));
        Assert.Equal([RouteActions.Fight, RouteActions.Harvest], Nodes(editor, 2));
        Assert.Equal([RouteActions.Harvest], Nodes(editor, 3));
    }

    [Fact]
    public async Task ADragPastTheEndsOfTheRouteKeepsTheClipInsideIt()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash), Step(3), Step(4));
        var sequencer = editor.Sequencer;

        sequencer.BeginClip(sequencer.Scene.Tracks[0].Clips[0].Id, SequencerEdge.None, 0, KeyModifiers.None);
        sequencer.Drag(40);
        sequencer.EndGesture();

        var clip = sequencer.Scene.Tracks[0].Clips[0];

        Assert.Equal((2, 3), (clip.First, clip.Last));
    }

    [Fact]
    public async Task DeletingTheClipSelectedTakesItsActionOffEveryStepItCoversAndUndoGivesItBack()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var fight = Call(RouteActions.Fight, new RouteFightValues());
        var (_, editor) = await OpenAsync(Step(1, ash, fight), Step(2, ash), Step(3));
        var sequencer = editor.Sequencer;

        sequencer.PressClip(sequencer.Scene.Tracks[0].Clips[0].Id);
        sequencer.RemoveSelectedClip();

        Assert.Equal([RouteActions.Fight], Nodes(editor, 0));
        Assert.Empty(Nodes(editor, 1));
        Assert.Equal(-1, sequencer.SelectedClip);

        editor.Undo();

        Assert.Equal([RouteActions.Harvest, RouteActions.Fight], Nodes(editor, 0));
        Assert.Equal([RouteActions.Harvest], Nodes(editor, 1));
    }

    [Fact]
    public async Task APressOnAClipNeverChangesTheRoute()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash));
        var sequencer = editor.Sequencer;

        sequencer.BeginClip(sequencer.Scene.Tracks[0].Clips[0].Id, SequencerEdge.None, 1, KeyModifiers.None);
        sequencer.EndGesture();

        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public async Task TheArrowsMoveTheStepChosenAndShiftReachesOutFromTheOneChosenFirst()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2), Step(3), Step(4));
        var sequencer = editor.Sequencer;

        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.None));
        Assert.Equal([0], sequencer.SelectedSteps);
        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.None));
        Assert.Equal([1], sequencer.SelectedSteps);
        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.Shift));
        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.Shift));
        Assert.Equal([1, 2, 3], sequencer.SelectedSteps);
        Assert.True(sequencer.PressKey(Key.Left, KeyModifiers.None));
        Assert.Equal([2], sequencer.SelectedSteps);
        Assert.True(sequencer.PressKey(Key.End, KeyModifiers.None));
        Assert.Equal([3], sequencer.SelectedSteps);
        Assert.True(sequencer.PressKey(Key.Home, KeyModifiers.Shift));
        Assert.Equal([0, 1, 2, 3], sequencer.SelectedSteps);
        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.None));
        Assert.Equal([1], sequencer.SelectedSteps);
    }

    [Fact]
    public async Task ControlASelectsEveryStepAndTheArrowsStopAtTheEnds()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2), Step(3));
        var sequencer = editor.Sequencer;

        Assert.True(sequencer.PressKey(Key.A, KeyModifiers.Control));
        Assert.Equal([0, 1, 2], sequencer.SelectedSteps);

        sequencer.PressKey(Key.End, KeyModifiers.None);
        sequencer.PressKey(Key.Right, KeyModifiers.None);

        Assert.Equal([2], sequencer.SelectedSteps);
    }

    [Fact]
    public async Task UpAndDownChooseTheClipsCoveringTheStepChosenOneAfterTheOtherAndEscapeLetsGo()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var fight = Call(RouteActions.Fight, new RouteFightValues());
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash, fight), Step(3, fight));
        var sequencer = editor.Sequencer;

        editor.Select(1);

        Assert.True(sequencer.PressKey(Key.Down, KeyModifiers.None));
        Assert.Equal(RouteActions.Harvest, sequencer.Selected!.Call.Node);
        Assert.True(sequencer.PressKey(Key.Down, KeyModifiers.None));
        Assert.Equal(RouteActions.Fight, sequencer.Selected!.Call.Node);
        Assert.True(sequencer.PressKey(Key.Up, KeyModifiers.None));
        Assert.Equal(RouteActions.Harvest, sequencer.Selected!.Call.Node);
        Assert.True(sequencer.PressKey(Key.Escape, KeyModifiers.None));
        Assert.Equal(-1, sequencer.SelectedClip);
        Assert.False(sequencer.PressKey(Key.Escape, KeyModifiers.None));
    }

    [Fact]
    public async Task AltWithTheArrowsMovesTheClipSelectedAndWithShiftOrControlMovesItsLastOrFirstEdgeEachOneAChangeToUndo()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash), Step(3), Step(4));
        var sequencer = editor.Sequencer;

        sequencer.PressClip(sequencer.Scene.Tracks[0].Clips[0].Id);

        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.Alt));
        Assert.Equal((1, 2), (sequencer.Selected!.First, sequencer.Selected.Last));
        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.Alt | KeyModifiers.Shift));
        Assert.Equal((1, 3), (sequencer.Selected!.First, sequencer.Selected.Last));
        Assert.True(sequencer.PressKey(Key.Left, KeyModifiers.Alt | KeyModifiers.Control));
        Assert.Equal((0, 3), (sequencer.Selected!.First, sequencer.Selected.Last));

        editor.Undo();

        Assert.Equal((1, 3), (sequencer.Scene.Tracks[0].Clips[0].First, sequencer.Scene.Tracks[0].Clips[0].Last));
    }

    [Fact]
    public async Task AltWithTheArrowsDoesNothingWithoutAClip()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2));

        Assert.False(editor.Sequencer.PressKey(Key.Right, KeyModifiers.Alt));
        Assert.False(editor.Sequencer.PressKey(Key.Delete, KeyModifiers.None));
    }

    [Fact]
    public async Task ControlDCopiesTheClipSelectedRightAfterItselfAndSelectsTheCopy()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash), Step(3), Step(4), Step(5));
        var sequencer = editor.Sequencer;

        sequencer.PressClip(sequencer.Scene.Tracks[0].Clips[0].Id);

        Assert.True(sequencer.PressKey(Key.D, KeyModifiers.Control));
        Assert.Equal([(0, 3)], sequencer.Scene.Tracks[0].Clips.Select(clip => (clip.First, clip.Last)));
        Assert.Equal([1, 1, 1, 1, 0], editor.CurrentAnchors.Select(anchor => anchor.Actions.Length));
        Assert.Equal((0, 3), (sequencer.Selected!.First, sequencer.Selected.Last));
    }

    [Fact]
    public async Task DeleteTakesTheClipSelectedOffAndTheStatusSaysWhatIsSelected()
    {
        var ash = Call(RouteActions.Harvest, new RouteHarvestValues { Resources = [1] });
        var (_, editor) = await OpenAsync(Step(1, ash), Step(2, ash), Step(3));
        var sequencer = editor.Sequencer;

        Assert.Equal("", sequencer.Status);

        editor.Select(1);

        Assert.Equal("Step 2 of 3", sequencer.Status);

        editor.SelectSteps([0, 1]);

        Assert.Equal("2 steps selected", sequencer.Status);

        sequencer.PressClip(sequencer.Scene.Tracks[0].Clips[0].Id);

        Assert.Contains("steps 1 to 2", sequencer.Status, StringComparison.Ordinal);

        Assert.True(sequencer.PressKey(Key.Delete, KeyModifiers.None));
        Assert.All(editor.CurrentAnchors, anchor => Assert.Empty(anchor.Actions));
    }

    private static NodeCall Default(string node)
    {
        var catalog = TestRouteWorld.Nodes();

        Assert.True(catalog.TryGet(node, out var definition));

        return RouteActionCalls.Create(definition);
    }

    private static NumberFieldViewModel Field(RouteActionViewModel card)
    {
        return Assert.IsType<NumberFieldViewModel>(Assert.Single(card.Editor!.Sections.SelectMany(section => section.Fields)));
    }

    [Fact]
    public async Task TheValuesOfTheClipSelectedAreSetOnceForEveryStepItCoversAndTheFormIsNotMadeAgainUnderTheHand()
    {
        var wait = Default(RouteActions.Wait);
        var (_, editor) = await OpenAsync(Step(1, wait), Step(2, wait), Step(3, wait), Step(4));
        var sequencer = editor.Sequencer;

        Assert.Null(sequencer.ClipCard);

        sequencer.PressClip(sequencer.Scene.Tracks[0].Clips[0].Id);

        var card = sequencer.ClipCard!;

        Assert.True(sequencer.HasClip);
        Assert.Equal("Wait 5 s", card.Sentence);

        Field(card).Value = 12;

        Assert.Same(card, sequencer.ClipCard);
        Assert.Equal(3, editor.CurrentAnchors.Take(3).Count(anchor => RouteAnchor.SameCall(anchor.Actions[0], editor.CurrentAnchors[0].Actions[0])));
        Assert.False(RouteAnchor.SameCall(editor.CurrentAnchors[0].Actions[0], wait));
        Assert.Single(sequencer.Scene.Tracks[0].Clips);
        Assert.Equal(sequencer.Scene.Tracks[0].Clips[0].Id, sequencer.SelectedClip);

        editor.Undo();

        Assert.True(RouteAnchor.SameCall(editor.CurrentAnchors[0].Actions[0], wait));

        sequencer.ClearClip();

        Assert.Null(sequencer.ClipCard);
    }

    [Fact]
    public async Task TheClipSelectedCanBeExtendedToTheWholeRouteAndItsCardsXRemovesIt()
    {
        var wait = Default(RouteActions.Wait);
        var (_, editor) = await OpenAsync(Step(1), Step(2, wait), Step(3), Step(4));
        var sequencer = editor.Sequencer;

        sequencer.PressClip(sequencer.Scene.Tracks[0].Clips[0].Id);
        sequencer.ExtendClipCommand.Execute(null);

        Assert.Equal([1, 1, 1, 1], editor.CurrentAnchors.Select(anchor => anchor.Actions.Length));
        Assert.Equal((0, 3), (sequencer.Selected!.First, sequencer.Selected.Last));

        sequencer.ClipCard!.RemoveCommand.Execute(null);

        Assert.All(editor.CurrentAnchors, anchor => Assert.Empty(anchor.Actions));
        Assert.Null(sequencer.ClipCard);
    }

    [Fact]
    public async Task SeveralStepsSelectedShowTheActionsAllOfThemHoldAndAValueIsSetOnThemAloneOnceForAll()
    {
        var wait = Default(RouteActions.Wait);
        var fight = Call(RouteActions.Fight, new RouteFightValues());
        var (_, editor) = await OpenAsync(Step(1, wait, fight), Step(2, wait), Step(3, wait, fight), Step(4, wait));
        var sequencer = editor.Sequencer;

        editor.SelectSteps([0, 2]);

        Assert.True(sequencer.HasSeveralSteps);
        Assert.Equal("Wait 5 s", sequencer.CommonActions[0].Sentence);
        Assert.StartsWith("Fight", sequencer.CommonActions[1].Sentence, StringComparison.Ordinal);
        Assert.Equal(2, sequencer.CommonActions.Count);

        var card = sequencer.CommonActions[0];

        Field(card).Value = 20;

        Assert.Same(card, sequencer.CommonActions[0]);
        Assert.False(RouteAnchor.SameCall(editor.CurrentAnchors[0].Actions[0], wait));
        Assert.True(RouteAnchor.SameCall(editor.CurrentAnchors[0].Actions[0], editor.CurrentAnchors[2].Actions[0]));
        Assert.True(RouteAnchor.SameCall(editor.CurrentAnchors[1].Actions[0], wait));
        Assert.True(RouteAnchor.SameCall(editor.CurrentAnchors[3].Actions[0], wait));
    }

    [Fact]
    public async Task SeveralStepsWithNothingInCommonSayItAndOneStepShowsNoCommonCards()
    {
        var wait = Default(RouteActions.Wait);
        var (_, editor) = await OpenAsync(Step(1, wait), Step(2), Step(3));

        editor.SelectSteps([0, 1]);

        Assert.True(editor.Sequencer.HasNoCommonAction);
        Assert.Empty(editor.Sequencer.CommonActions);

        editor.Select(0);

        Assert.False(editor.Sequencer.HasNoCommonAction);
        Assert.Empty(editor.Sequencer.CommonActions);
    }

    [Fact]
    public async Task EditingTheSecondOfTheActionsStepsShareChangesThatActionAndNotTheFirst()
    {
        var wait = Default(RouteActions.Wait);
        var fight = Call(RouteActions.Fight, new RouteFightValues());
        var (_, editor) = await OpenAsync(Step(1, wait, fight), Step(2, wait), Step(3, wait, fight));
        var sequencer = editor.Sequencer;

        editor.SelectSteps([0, 2]);

        var card = sequencer.CommonActions[1];
        var least = card.Editor!.Sections.SelectMany(section => section.Fields).OfType<NumberFieldViewModel>().First();

        least.Value = 2;

        Assert.All([0, 2], step =>
        {
            Assert.True(RouteAnchor.SameCall(editor.CurrentAnchors[step].Actions[0], wait));
            Assert.Equal(RouteActions.Fight, editor.CurrentAnchors[step].Actions[1].Node);
            Assert.False(RouteAnchor.SameCall(editor.CurrentAnchors[step].Actions[1], fight));
        });
        Assert.Same(card, sequencer.CommonActions[1]);
    }

    [Fact]
    public async Task AClipMovedToOtherStepsGetsItsCardMadeAgainForTheMapOfItsFirstStep()
    {
        var door = Call(RouteActions.Door, new RouteDoorValues { Cell = 312 });
        var (_, editor) = await OpenAsync(Step(1, door), Step(2, door), Step(3), Step(4));
        var sequencer = editor.Sequencer;

        sequencer.PressClip(sequencer.Scene.Tracks[0].Clips[0].Id);

        var before = sequencer.ClipCard;

        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.Alt));
        Assert.True(sequencer.PressKey(Key.Right, KeyModifiers.Alt));

        Assert.NotSame(before, sequencer.ClipCard);
    }

    private static FollowedRun Following(string name, string route, int anchor, bool arrived, int anchors = 3)
    {
        var position = new RouteRunPosition(new CharacterKey(1, anchor), route, route, RouteMode.OneWay, anchors, anchor, arrived, 0, 0, 0, false);

        return new FollowedRun(position, name, name[..2].ToUpperInvariant(), null);
    }

    [Fact]
    public async Task ThePlayheadOfACharacterOnTheRouteIsOnTheStepItGoesToAndFilledOnceItIsThere()
    {
        var (routes, editor) = await OpenAsync(Step(1), Step(2), Step(3));
        var runs = new List<FollowedRun> { Following("Ayuwoki", "r1", 1, false) };

        routes.Followed = new FollowedRuns(() => runs);
        editor.Sequencer.Follow();

        Assert.Equal([(1, "AY", false)], editor.Sequencer.Scene.Playheads.Select(head => (head.Step, head.Initials, head.Arrived)));
        Assert.Equal("Ayuwoki · to step 2/3", editor.Sequencer.RunStatus);

        runs[0] = Following("Ayuwoki", "r1", 1, true);
        editor.Sequencer.Follow();

        Assert.Equal([(1, "AY", true)], editor.Sequencer.Scene.Playheads.Select(head => (head.Step, head.Initials, head.Arrived)));
        Assert.Equal("Ayuwoki · step 2/3", editor.Sequencer.RunStatus);

        runs.Clear();
        editor.Sequencer.Follow();

        Assert.Empty(editor.Sequencer.Scene.Playheads);
        Assert.Equal("", editor.Sequencer.RunStatus);
    }

    [Fact]
    public async Task EachCharacterOnTheRouteHasItsOwnPlayheadAndTheRunOfAnotherRouteIsNotShown()
    {
        var (routes, editor) = await OpenAsync(Step(1), Step(2), Step(3));
        var runs = new List<FollowedRun> { Following("Ayuwoki", "r1", 0, true), Following("Bolgrim", "r1", 2, false), Following("Cael", "other", 1, true) };

        routes.Followed = new FollowedRuns(() => runs);
        editor.Sequencer.Follow();

        Assert.Equal([(0, "AY"), (2, "BO")], editor.Sequencer.Scene.Playheads.Select(head => (head.Step, head.Initials)));
        Assert.Equal("Ayuwoki · step 1/3, Bolgrim · to step 3/3", editor.Sequencer.RunStatus);
    }

    [Fact]
    public async Task ThePlayheadOfARunPastTheStepsTheRouteHasNowIsNotShown()
    {
        var (routes, editor) = await OpenAsync(Step(1), Step(2), Step(3));
        var runs = new List<FollowedRun> { Following("Ayuwoki", "r1", 4, true, 5), Following("Bolgrim", "r1", 1, true, 5) };

        routes.Followed = new FollowedRuns(() => runs);
        editor.Sequencer.Follow();

        Assert.Equal([1], editor.Sequencer.Scene.Playheads.Select(head => head.Step));
        Assert.Equal("Bolgrim · step 2/5", editor.Sequencer.RunStatus);
    }

    [Fact]
    public async Task ThePlayheadsSurviveAnEditOfTheRouteAndLeaveWhenTheirStepIsGone()
    {
        var (routes, editor) = await OpenAsync(Step(1), Step(2), Step(3));
        var runs = new List<FollowedRun> { Following("Ayuwoki", "r1", 1, true) };

        routes.Followed = new FollowedRuns(() => runs);
        editor.Sequencer.Follow();
        editor.Remove(2);

        Assert.Equal([1], editor.Sequencer.Scene.Playheads.Select(head => head.Step));

        editor.Remove(1);

        Assert.Empty(editor.Sequencer.Scene.Playheads);
    }

    [Fact]
    public async Task TheSequencerKeepsThePlayheadInSightByDefault()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2));

        Assert.True(editor.Sequencer.KeepsInSight);
    }

    [Fact]
    public async Task HoveringAColumnTellsWhichStepToLightAndLeavingItTellsNone()
    {
        var (_, editor) = await OpenAsync(Step(1), Step(2));
        var lit = new List<int>();

        editor.Sequencer.StepLit += lit.Add;
        editor.Sequencer.Hover(1);
        editor.Sequencer.Hover(1);
        editor.Sequencer.Hover(-1);

        Assert.Equal([1, -1], lit);
    }
}
