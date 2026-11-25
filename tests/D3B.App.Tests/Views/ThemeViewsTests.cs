using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.App.Views;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Tests.Behaviors;

namespace D3B.App.Tests.Views;

public sealed class ThemeViewsTests : IAsyncDisposable
{
    private readonly TestStore _store = new();

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ThemeViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static Color? ColorOf(IBrush? brush)
    {
        return (brush as ISolidColorBrush)?.Color;
    }

    [Fact]
    public async Task EveryPartOfTheStudioIsDrawnOnNextsIslands()
    {
        await _store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await _store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, TestStore.Settings(new TestSettings { Walks = 3 }), ct);

        await _store.Assets.SaveAsync(new RouteAsset { Id = "north", Name = "North", Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }] }, ct);
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));

        var runner = await TestHosts.RunningAsync(_store.Host);
        var frames = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;
            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
            var frames = new List<(Color? Window, Color? Dock, Color? Editor)>();

            localizer.Use(Localizer.French);
            main.Routes.Use(TestRouteWorld.Services(_store.Assets));
            main.Refresh();
            window.Show();

            void Draw(string theme)
            {
                main.Layout.Reveal(SideView.Characters);
                main.ShowHome();
                main.Refresh();
                Dispatcher.UIThread.RunJobs();
                Drawings.Draw(window, "theme-home-" + theme);

                var docks = window.GetVisualDescendants().OfType<Border>().ToList();

                frames.Add((ColorOf(window.Background), ColorOf(docks.Single(border => border.Name == "SideDock").Background), ColorOf(docks.Single(border => border.Name == "EditorDock").Background)));

                main.OpenCharacter(TestPlay.Key);
                main.Refresh();
                Dispatcher.UIThread.RunJobs();
                Drawings.Draw(window, "theme-character-" + theme);

                main.Layout.Reveal(SideView.Library);
                main.OpenAsset(walker.Id);
                main.Refresh();
                Dispatcher.UIThread.RunJobs();
                Drawings.Draw(window, "theme-profile-" + theme);

                main.Layout.Reveal(SideView.World);
                main.OpenAsset("north");
                main.Refresh();
                Dispatcher.UIThread.RunJobs();
                Drawings.Draw(window, "theme-world-" + theme);

                main.ToggleJournal();
                main.ShowStatistics();
                main.Refresh();
                Dispatcher.UIThread.RunJobs();
                Drawings.Draw(window, "theme-statistics-" + theme);

                main.ShowSettings();
                main.Refresh();
                Dispatcher.UIThread.RunJobs();
                Drawings.Draw(window, "theme-settings-" + theme);

                main.Search.Open();
                Dispatcher.UIThread.RunJobs();
                Drawings.Draw(window, "theme-search-" + theme);
                main.Search.Close();
                main.ToggleJournal();
                main.Layout.Close(StudioPanel.Details);
            }

            Draw("dark");
            localizer.Use(Localizer.English);
            window.Close();

            return frames;
        });

        // next's islands: the frame darker than its parts, the editor lighter (D530).
        Assert.Equal((Color.Parse("#111111"), Color.Parse("#181818"), Color.Parse("#1F1F1F")), Assert.Single(frames));
    }
}
