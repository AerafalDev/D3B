using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.Views;
using D3B.Bot.Tests.Runtime;
using D3B.Studio.Assets;

namespace D3B.App.Tests.Views;

public sealed class HomeViewTests : IAsyncDisposable
{
    private readonly TestStore _store = new();

    private static List<string> Texts(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HomeViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    [Fact]
    public async Task TheStudioOpensOnTheHomePageInBothLanguagesAndTheWorldAppearsOnceARouteIsSelected()
    {
        await _store.OpenedAsync();
        await _store.Assets.SaveAsync(new RouteAsset { Id = "north", Name = "North", Anchors = [new RouteAnchor { MapId = 1 }, new RouteAnchor { MapId = 2 }] }, TestContext.Current.CancellationToken);
        await _store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));

        var runner = await TestHosts.RunningAsync(_store.Host);
        var result = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Routes.Use(TestRouteWorld.Services(_store.Assets));
            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var home = window.GetVisualDescendants().OfType<HomeView>().Single(view => view.IsEffectivelyVisible);
            var english = Texts(home);

            Drawings.Draw(window, "home-en");

            var worldAtStart = window.GetVisualDescendants().OfType<WorldView>().Any(view => view.IsEffectivelyVisible);

            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var french = Texts(home);

            Drawings.Draw(window, "home-fr");
            localizer.Use(Localizer.English);
            main.Refresh();
            main.Outliner.Select(main.Outliner.Rows.Single(row => row.Key == "route:north"));
            Dispatcher.UIThread.RunJobs();

            var shown = (window.GetVisualDescendants().OfType<WorldView>().Any(view => view.IsEffectivelyVisible), home.IsEffectivelyVisible);

            window.Close();

            return (english, french, worldAtStart, shown);
        });

        Assert.Contains("D3B Studio", result.english);
        Assert.Contains("Quick actions", result.english);
        Assert.Contains("New route", result.english);
        Assert.Contains("Aerafa", result.english);
        Assert.Contains("North", result.english);
        Assert.DoesNotContain(result.english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Actions rapides", result.french);
        Assert.Contains("Nouvelle route", result.french);
        Assert.Contains("Récents", result.french);
        Assert.DoesNotContain(result.french, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.False(result.worldAtStart);
        Assert.Equal((true, false), result.shown);
    }
}
