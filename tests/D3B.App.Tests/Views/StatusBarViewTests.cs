using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.Views;

public sealed class StatusBarViewTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StatusBarViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static List<string> Texts(Control root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheStatusBarSaysTheVersionTheRelayTheClientTheBotsAndTheProblemsAndItsProblemsOpenTheirTab()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (right, relay, tab) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.French);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var bar = window.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "StatusBar");
            // Every item on the right, in icons whose words are their tooltips (D546).
            var side = bar.Children.OfType<StackPanel>().Single();

            Drawings.DrawPart(bar, "part-status-bar");

            var right = Texts(side);
            // A tooltip's words are bound once it shows: the view model's are the ones it would show.
            var relay = side.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "RelayItem") ? main.StatusBar.RelayHover : null;

            side.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ProblemsItem").Command!.Execute(null);

            var tab = main.Bottom.Tab;

            window.Close();

            return (right, relay, tab);
        });

        Assert.StartsWith("Relais prêt · 1 en jeu", relay, StringComparison.Ordinal);
        Assert.DoesNotContain("D3B " + D3B.App.Updates.AppRelease.Version, right);
        // Icons and figures on the right, their words in the tooltips (D544).
        Assert.Contains("0", right);
        Assert.DoesNotContain("Tout est enregistré", right);
        Assert.Equal(BottomTab.Problems, tab);
    }
}
