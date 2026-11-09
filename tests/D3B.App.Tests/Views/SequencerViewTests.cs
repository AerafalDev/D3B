using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls.Sequencer;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.App.Views;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.App.Tests.Views;

public sealed class SequencerViewTests
{
    private static Task<T> OnTheUIThreadAsync<T>(Func<Task<T>> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SequencerViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static NodeCall Call(string node)
    {
        return new NodeCall(node, ImmutableSortedDictionary<string, JsonNode?>.Empty);
    }

    [Fact]
    public async Task SelectingARouteShowsItsSequencerInTheBottomPanelAndAPressOnItsRulerAndClipsSelectsThem()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
        var harvest = Call(RouteActions.Harvest);
        var fight = Call(RouteActions.Fight);

        await store.Assets.SaveAsync(
            new RouteAsset { Id = "r1", Name = "North", Anchors = [new RouteAnchor { MapId = 1, Actions = [harvest] }, new RouteAnchor { MapId = 2, Actions = [harvest, fight] }, new RouteAnchor { MapId = 3, Actions = [harvest] }, new RouteAnchor { MapId = 4 }] },
            TestContext.Current.CancellationToken);

        var result = await OnTheUIThreadAsync(async () =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);

            main.Refresh();
            main.Routes.Use(TestRouteWorld.Services(store.Assets));

            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var before = (main.Bottom.HasSequencer, main.Bottom.Tab);

            main.World.Refresh();
            main.Selection.SelectRoute("r1");
            Dispatcher.UIThread.RunJobs();

            var shown = (main.Bottom.HasSequencer, main.Bottom.Tab, main.Bottom.IsSequencerShown);
            var view = window.GetVisualDescendants().OfType<SequencerView>().Single(item => item.IsEffectivelyVisible);
            var grid = view.GetVisualDescendants().OfType<SequencerGrid>().Single();
            var scene = (grid.Scene.Steps.Count, grid.Scene.Tracks.Select(track => track.Node).ToList());
            var sequencer = main.Bottom.Sequencer!;

            // A press on the ruler's third step chooses it; with Control a press on the first adds it.
            var third = grid.TranslatePoint(grid.RectOfStep(2).Center, window)!.Value;
            var first = grid.TranslatePoint(grid.RectOfStep(0).Center, window)!.Value;

            window.MouseDown(third, MouseButton.Left);
            window.MouseUp(third, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var chosen = sequencer.SelectedSteps.ToList();

            window.MouseDown(first, MouseButton.Left, RawInputModifiers.Control);
            window.MouseUp(first, MouseButton.Left, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            var several = sequencer.SelectedSteps.ToList();

            // A press on the fight clip selects it and leaves the steps.
            var fightClip = sequencer.Scene.Tracks.Single(track => track.Node == RouteActions.Fight).Clips.Single();
            var clipAt = grid.TranslatePoint(grid.RectOfClip(fightClip.Id)!.Value.Center, window)!.Value;

            window.MouseDown(clipAt, MouseButton.Left);
            window.MouseUp(clipAt, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var clip = (sequencer.SelectedClip == fightClip.Id, sequencer.SelectedSteps.ToList());

            Drawings.Draw(window, "sequencer");

            // Another selection takes the tab away and the journal comes back.
            main.Selection.SelectZone("none");
            Dispatcher.UIThread.RunJobs();

            var gone = (main.Bottom.HasSequencer, main.Bottom.Tab);

            window.Close();

            return (before, shown, scene, chosen, several, clip, gone);
        });

        Assert.Equal((false, BottomTab.Journal), result.before);
        Assert.Equal((true, BottomTab.Sequencer, true), result.shown);
        Assert.Equal(4, result.scene.Item1);
        Assert.Equal([RouteActions.Harvest, RouteActions.Fight], result.scene.Item2);
        Assert.Equal([2], result.chosen);
        Assert.Equal([0, 2], result.several);
        Assert.True(result.clip.Item1);
        Assert.Equal([0, 2], result.clip.Item2);
        Assert.Equal((false, BottomTab.Journal), result.gone);
    }

    [Fact]
    public async Task APressAndADragOnATrackDrawsAClipAndADragOnItsEdgeStretchesItWithThePointer()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);

        await store.Assets.SaveAsync(
            new RouteAsset { Id = "r1", Name = "North", Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }, new RouteAnchor { MapId = 3 }, new RouteAnchor { MapId = 4 }] },
            TestContext.Current.CancellationToken);

        var result = await OnTheUIThreadAsync(async () =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);

            main.Refresh();
            main.Routes.Use(TestRouteWorld.Services(store.Assets));

            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            window.Show();
            main.World.Refresh();
            main.Selection.SelectRoute("r1");
            Dispatcher.UIThread.RunJobs();

            var sequencer = main.Bottom.Sequencer!;

            sequencer.AddTrack(RouteActions.Fight);
            Dispatcher.UIThread.RunJobs();

            var grid = window.GetVisualDescendants().OfType<SequencerGrid>().Single(item => item.IsEffectivelyVisible);
            var row = SequencerLayout.RulerHeight + SequencerLayout.MovementHeight + 10;
            Point At(int step, double edge = 0.5)
            {
                return grid.TranslatePoint(new Point(grid.ColumnWidth * (step + edge), row), window)!.Value;
            }

            // Drawn from the second step to the third.
            window.MouseDown(At(1), MouseButton.Left);
            window.MouseMove(At(2));
            window.MouseUp(At(2), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var drawn = main.World.Route!.CurrentAnchors.Select(anchor => anchor.Actions.Length).ToList();

            // Its right edge dragged to the fourth.
            window.MouseDown(At(2, 0.97), MouseButton.Left);
            window.MouseMove(At(3));
            window.MouseUp(At(3), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var stretched = main.World.Route!.CurrentAnchors.Select(anchor => anchor.Actions.Length).ToList();

            // Delete takes the clip off.
            grid.Focus();
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            var deleted = main.World.Route!.CurrentAnchors.Select(anchor => anchor.Actions.Length).ToList();

            window.Close();

            return (drawn, stretched, deleted);
        });

        Assert.Equal([0, 1, 1, 0], result.drawn);
        Assert.Equal([0, 1, 1, 1], result.stretched);
        Assert.Equal([0, 0, 0, 0], result.deleted);
    }
}
