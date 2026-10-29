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
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.Views;

public sealed class MainWindowTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MainWindowTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
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

    [Fact]
    public async Task TheSamplePluginsPageShowsInTheRailInTheAppsLanguage()
    {
        // The sample loads as a host loads it: from its folder, into a context of its own, sharing the app's Avalonia.
        using var host = TestHosts.WithPlugins(_play, TestHosts.SamplePlugins);
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (titles, english, french, bots) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main };

            main.Refresh();
            window.Show();

            var page = Assert.Single(main.Pages);
            var titles = new List<string> { page.Title };

            page.ShowCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var english = Texts(window);

            main.SwitchLanguageCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            titles.Add(page.Title);

            var french = Texts(window);

            main.ShowBotsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var bots = Texts(window);

            localizer.Use(Localizer.English);
            window.Close();

            return (titles, english, french, bots);
        });

        Assert.Equal(["Hello", "Bonjour"], titles);
        Assert.Contains("Not strolling", english);
        Assert.Contains("None yet", english);
        Assert.Contains("Level 200", english);
        Assert.Contains("walks arrived", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Bonjour", french);
        Assert.Contains("Pas en promenade", french);
        Assert.Contains("Aucune encore", french);
        Assert.Contains("Dernière marche", french);
        Assert.Contains("marche arrivée", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Promenade", bots);
    }

    [Fact]
    public async Task TheSamplePluginsStrollWalksAndItsPageShowsIt()
    {
        using var host = TestHosts.WithPlugins(_play, TestHosts.SamplePlugins);
        var runner = await TestHosts.RunningAsync(host);
        var (connection, _) = await _play.EnterAsync(300);
        var main = new MainViewModel(runner, Localizer.Instance);

        await OnTheUIThreadAsync(() =>
        {
            Localizer.Instance.Use(Localizer.English);
            main.Refresh();
            Assert.Single(main.Bots.Items).StartCommand.Execute(null);

            return true;
        });

        var cell = await TestWalks.WalkedAsync(_play, connection, 1);

        // The stroll goes on with its next walk once the first is recorded.
        await TestWalks.SentAsync(_play, connection, 2);

        var texts = await OnTheUIThreadAsync(() =>
        {
            var window = new MainWindow { DataContext = main };

            main.Refresh();
            window.Show();
            main.Pages[0].ShowCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var texts = Texts(window);

            window.Close();

            return texts;
        });

        Assert.Contains("Strolling", texts);
        Assert.Contains("1", texts);
        Assert.Contains("walk arrived", texts);
        Assert.Contains($"Cell {cell}, arrived", texts);
        Assert.Contains("This stroll", texts);
        Assert.Contains("1 of 10", texts);
    }

    [Fact]
    public async Task TheAccountsAndProfilesPagesShowInTheRailInBothLanguages()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await store.Profiles.SaveAsync(0, "Walker", TestStore.HarvestId, TestStore.Settings(new() { Job = D3B.Bot.Tests.Settings.TestReferences.Lumberjack }), ct);

        await store.SeeAsync(TestPlay.Account, (TestPlay.CharacterId, "Aerafa", 200));
        await store.Accounts.AssignAsync(TestPlay.Key, walker.Id, ct);

        var runner = await TestHosts.RunningAsync(store.Host);
        var (english, french) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main };
            var texts = new List<string>();

            main.Refresh();
            window.Show();
            main.ShowAccountsCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            main.ShowProfilesCommand.Execute(null);
            main.Profiles.Items[0].EditCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));

            var english = texts.ToList();

            texts.Clear();
            main.SwitchLanguageCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            main.ShowAccountsCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            localizer.Use(Localizer.English);
            window.Close();

            return (english, texts);
        });

        Assert.Contains("Accounts", english);
        Assert.Contains("Aerafa", english);
        Assert.Contains("Walker", english);
        Assert.Contains("On connection", english);
        Assert.Contains("Profiles", english);
        Assert.Contains("New profile", english);
        Assert.Contains(english, text => text.Contains("Lumberjack", StringComparison.Ordinal));
        Assert.Contains("Save", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Comptes", french);
        Assert.Contains("Profils", french);
        Assert.Contains("Nouveau profil", french);
        Assert.Contains(french, text => text.Contains("Bûcheron", StringComparison.Ordinal));
        Assert.Contains("Enregistrer", french);
        Assert.Contains("À la connexion", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
    }
}
