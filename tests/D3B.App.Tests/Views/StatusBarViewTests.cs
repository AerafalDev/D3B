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

        var (left, right, tab) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.French);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var bar = window.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "StatusBar");
            var sides = bar.Children.OfType<StackPanel>().ToList();

            Drawings.DrawPart(bar, "part-status-bar");

            var left = Texts(sides[0]);
            var right = Texts(sides[1]);

            sides[1].GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ProblemsItem").Command!.Execute(null);

            var tab = main.Bottom.Tab;

            window.Close();

            return (left, right, tab);
        });

        Assert.Equal(["D3B " + D3B.App.Updates.AppRelease.Version, "Relais prêt · 1 en jeu"], left);
        Assert.Contains("0 bot actif", right);
        Assert.Contains("Tout est enregistré", right);
        Assert.Equal(BottomTab.Problems, tab);
    }
}
