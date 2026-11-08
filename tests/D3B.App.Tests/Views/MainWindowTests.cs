using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.Tests.Pages;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Studio;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Inspection;
using D3B.Bot.Pages;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using D3B.Studio.Tests.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    public async Task TheWindowShowsTheCharactersAndFollowsAChangeOfLanguage()
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

                Drawings.Draw(window, "characters-en");
                main.Settings.UseFrenchCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();

                var french = Texts(window);

                Drawings.Draw(window, "characters-fr");
                localizer.Use(Localizer.English);
                window.Close();

                return (english, french);
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("Characters", english);
        Assert.Contains("Welcome", english);
        Assert.Contains("Character", english);
        Assert.Contains("200", english);
        Assert.Contains("Stopped", english);
        // Connected goes without saying: only a character that is not says it.
        Assert.DoesNotContain("Connected", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Personnages", french);
        Assert.Contains("Bienvenue", french);
        Assert.Contains("Arrêté", french);
        Assert.Contains("En marche", french);
        Assert.DoesNotContain("Stopped", french);
    }

    [Fact]
    public async Task EachPanelClosesByItsCrossOpensFromTheWindowMenuAndTheEditorKeepsATabPerPage()
    {
        using var host = TestHosts.Of(_play);
        var runner = await TestHosts.RunningAsync(host);

        var (closed, reopened, tabs) = await OnTheUIThreadAsync(() =>
        {
            var main = new MainViewModel(runner, Localizer.Instance);
            var window = new MainWindow { DataContext = main };

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var inspector = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "InspectorDock");
            var cross = inspector.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("TabClose"));

            cross.Command!.Execute(cross.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            var closed = (inspector.IsEffectivelyVisible, window.FindControl<Grid>("Docks")!.ColumnDefinitions[4].Width.Value);
            var item = window.GetVisualDescendants().OfType<Menu>().Single().GetLogicalDescendants().OfType<MenuItem>().Single(menu => Equals(menu.CommandParameter, StudioPanel.Inspector));

            item.Command!.Execute(item.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            var reopened = (inspector.IsEffectivelyVisible, item.IsChecked);

            main.ShowSettingsCommand.Execute(null);
            main.ShowWelcomeCommand.Execute(null);
            main.ShowSettingsCommand.Execute(null);
            main.Editor.CloseCommand.Execute(main.Editor.Items.Single(tab => tab.Key == "welcome"));

            var tabs = (main.Editor.Items.Select(tab => tab.Key).ToList(), main.Editor.Selected?.Key);

            window.Close();

            return (closed, reopened, tabs);
        });

        Assert.Equal((false, 0d), closed);
        Assert.Equal((true, true), reopened);
        Assert.Equal(["settings"], tabs.Item1);
        Assert.Equal("settings", tabs.Item2);
    }

    [Fact]
    public async Task AModulesPageOpensInATabInTheAppsLanguage()
    {
        using var host = TestHosts.Of(_play, [TestPage.Definition()], new ActivityDefinition("patrol", "Patrol", _ => new TestActivity()));
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (titles, english, french, shown) = await OnTheUIThreadAsync(() =>
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

            var control = Assert.Single(window.GetVisualDescendants().OfType<TestPage>());
            var english = Texts(window);
            var shown = new List<bool> { control.IsEffectivelyVisible };

            Drawings.Draw(window, "module-en");

            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            titles.Add(page.Title);

            var french = Texts(window);

            main.ShowWelcomeCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            shown.Add(control.IsEffectivelyVisible);
            localizer.Use(Localizer.English);
            window.Close();

            return (titles, english, french, shown);
        });

        Assert.Equal(["Greetings", "Salutations"], titles);
        Assert.Contains("Hello from a module", english);
        Assert.Contains("1 bot in the host", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Bonjour d'un module", french);
        Assert.Contains("1 bot dans l'hôte", french);
        Assert.DoesNotContain("Hello from a module", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Equal([true, false], shown);
    }

    [Fact]
    public async Task TheInspectorShowsTheChosenCharacterAndTheProfilesOpenInATabInBothLanguages()
    {
        await using var store = new TestStore();

        await store.OpenedAsync();

        var ct = TestContext.Current.CancellationToken;
        var walker = await store.Assets.SaveProfileAsync("Walker", TestStore.HarvestId, TestStore.Settings(new() { Job = D3B.Bot.Tests.Settings.TestReferences.Lumberjack }), ct);

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
            main.RunBlock.Choose(TestPlay.Key);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "inspector-en");
            main.Refresh();
            main.OpenProfile(main.Profiles.Items.Single(profile => profile.Name == "Walker").Id);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));

            var english = texts.ToList();

            texts.Clear();
            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "profiles-edit-fr");
            main.ShowWelcomeCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "inspector-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (english, texts);
        });

        Assert.Contains("Inspector", english);
        Assert.Contains("Aerafa", english);
        Assert.Contains("Walker", english);
        Assert.Contains("Auto start", english);
        Assert.Contains("nickname#tag", english);
        Assert.Contains("Variant of", english);
        Assert.Contains("Preview", english);
        Assert.Contains("Save", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Inspecteur", french);
        Assert.Contains("Variante de", french);
        Assert.Contains("Aperçu", french);
        Assert.Contains("Enregistrer", french);
        Assert.Contains("Auto-démarrage", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
    }

    [Fact]
    public async Task TheJournalAndTheProtocolShowInTheBottomPanelInBothLanguagesAndACardOpensItsBotsJournal()
    {
        var recorder = new FrameRecorder(TimeProvider.System);

        await using var play = new TestPlay([recorder]);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(play.Bots);
        builder.Services.AddSingleton(play.World);
        builder.Services.AddSingleton(recorder);
        builder.Services.AddSingleton(new HostProblems(TimeProvider.System));
        builder.Services.AddSingleton(new ActivityCatalog([]));
        builder.Services.AddSingleton(new PageCatalog([]));

        using var host = builder.Build();
        var runner = await TestHosts.RunningAsync(host);

        await play.EnterAsync(300);

        var (english, french, journal) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main };
            var texts = new List<string>();

            main.Refresh();
            window.Show();
            main.Bots.Items[0].ShowJournalCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var journal = (main.Layout.IsBottomOpen && main.Bottom.IsJournalShown, main.Bottom.Logs.Source?.Name);

            texts.AddRange(Texts(window));
            Drawings.Draw(window, "logs-en");
            main.Bottom.ShowCommand.Execute(BottomTab.Protocol);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "inspector-en");

            var english = texts.ToList();

            texts.Clear();
            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "inspector-fr");
            main.Bottom.ShowCommand.Execute(BottomTab.Journal);
            Dispatcher.UIThread.RunJobs();
            texts.AddRange(Texts(window));
            Drawings.Draw(window, "logs-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (english, texts, journal);
        });

        Assert.Equal((true, "Character"), journal);
        Assert.Contains("Journal", english);
        Assert.Contains("Plays through the game connection", english);
        Assert.Contains("Warnings", english);
        Assert.Contains("Protocol", english);
        Assert.Contains(nameof(AuthenticationRequest), english);
        Assert.Contains("understood", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Protocole", french);
        Assert.Contains("compris", french);
        Assert.Contains("Journal", french);
        Assert.Contains("Joue par la connexion de jeu", french);
        Assert.Contains("Avertissements", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
    }

    [Fact]
    public async Task ACardOpensItsCharactersTabItsMapLiveInBothLanguages()
    {
        using var game = new TestGameData();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.Maps);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(game.Texts);
        builder.Services.AddSingleton(game.Tables);
        builder.Services.AddSingleton(new ActivityCatalog([]));
        builder.Services.AddSingleton(new PageCatalog([]));

        using var host = builder.Build();
        var runner = await TestHosts.RunningAsync(host);

        await _play.EnterAsync(300);

        var (english, french, map) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main };

            main.Refresh();
            window.Show();
            main.Bots.Items[0].ShowMapCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var tab = (CharacterTabViewModel)main.Editor.Selected!.Content;
            var map = (main.Editor.IsShown("character:" + TestPlay.Key), main.Editor.Items.Count, tab.Name, tab.Map.Scene?.Character);
            var english = Texts(window);

            Drawings.Draw(window, "character-tab-en");

            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var french = Texts(window);

            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, map);
        });

        Assert.Equal((true, 2, "Character", 300), map);
        Assert.Contains("Character", english);
        Assert.Contains("4,-18", english);
        Assert.Contains("Sub-area 440, cell 300", english);
        Assert.Contains("Layers", english);
        Assert.Contains("Behavior", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Comportement", french);
        Assert.Contains("Sous-zone 440, cellule 300", french);
        Assert.Contains("Calques", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
    }
}
