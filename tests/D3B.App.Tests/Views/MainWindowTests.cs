using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.Views;

public sealed class MainWindowTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheWindowShowsTheBotsAndFollowsAChangeOfLanguage()
    {
        using var host = TestHosts.Of(_play, new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MainWindowTests).Assembly);

        // The views bind to the app's own texts, which only this test changes, and gives back.
        var (english, french) = await session.Dispatch(
            () =>
            {
                var localizer = Localizer.Instance;

                localizer.Use(Localizer.English);

                var main = new MainViewModel(runner, localizer);
                var window = new MainWindow { DataContext = main };

                main.Refresh();
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var english = Texts(window);

                main.SwitchLanguageCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();

                var french = Texts(window);

                localizer.Use(Localizer.English);
                window.Close();

                return (english, french);
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("Bots", english);
        Assert.Contains("Character", english);
        Assert.Contains("Level 200", english);
        Assert.Contains("Connected", english);
        Assert.Contains("Patrol", english);
        Assert.Contains("Start", english);
        Assert.Contains("EN", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Niveau 200", french);
        Assert.Contains("Connecté", french);
        Assert.Contains("Démarrer", french);
        Assert.Contains("En marche", french);
        Assert.Contains("FR", french);
        Assert.DoesNotContain("Start", french);
    }
}
