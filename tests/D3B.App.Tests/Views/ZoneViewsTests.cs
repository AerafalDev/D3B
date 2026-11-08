using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Zones;
using D3B.App.Views;
using D3B.Studio.Assets;

namespace D3B.App.Tests.Views;

public sealed class ZoneViewsTests
{
    // The language is the window's one, shared by every test of the session: it is set, and read, on the interface's thread alone.
    private static Task<T> OnTheUIThreadAsync<T>(Func<Task<T>> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ZoneViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AZoneOpensInATabOnTheWorldsMapAndTheKeysChooseItsToolsAndTheTabFollowsTheEditor()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var runner = await TestHosts.RunningAsync(store.Host);
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

            var editor = (await main.Zones.CreateAsync(""))!;

            Dispatcher.UIThread.RunJobs();

            var view = window.GetVisualDescendants().OfType<ZoneView>().Single(item => item.IsEffectivelyVisible);
            var map = view.GetVisualDescendants().OfType<WorldMapView>().Single();

            editor.TraceSquare(1, 0, isDrag: false);
            Dispatcher.UIThread.RunJobs();

            var painted = map.Scene.Squares!.Count;

            map.Focus();
            window.KeyPressQwerty(PhysicalKey.E, RawInputModifiers.None);

            var erasing = (editor.Tool, map.Tool);

            window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.None);

            var subArea = (editor.Tool, map.Tool);

            window.KeyPressQwerty(PhysicalKey.B, RawInputModifiers.None);

            var painting = editor.Tool;

            // Undo and Redo of the window's Edit follow the zone shown.
            var title = main.Editor.Selected?.Content;

            main.Undo();

            var undone = (main.Editor.Selected?.Content == editor, map.Scene.Squares!.Count, editor.IsDirty);

            window.Close();

            return (painted, erasing, subArea, painting, title, undone, main.Assets.Kinds.Single(kind => kind.Kind is AssetKind.Zone).IsEnabled);
        });

        Assert.Equal(1, result.painted);
        Assert.Equal((WorldMapTool.Erase, WorldMapTool.Erase), result.erasing);
        Assert.Equal((WorldMapTool.Region, WorldMapTool.Region), result.subArea);
        Assert.Equal(WorldMapTool.Trace, result.painting);
        Assert.IsType<ZoneEditorViewModel>(result.title);
        Assert.Equal((true, 0, false), result.undone);
        Assert.True(result.IsEnabled);
    }
}
