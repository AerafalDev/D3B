using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Avalonia.Input;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
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
}
