using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Fighting;
using D3B.Bot.Hosting;
using D3B.Bot.Pages;
using D3B.Bot.Settings;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Data;
using D3B.Data.Tests;
using D3B.Storage;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

/// <summary>The fighting activity as the app edits it, its monsters chosen with their icons, and the accounts page with no strategy (D236, D244).</summary>
public sealed class FightViewsTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();
    private readonly TemporaryDirectory _directory = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FightViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _play.DisposeAsync();
        _game.Dispose();
        _directory.Dispose();
    }

    private async Task<IHost> HostAsync()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.Maps);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(_play.Tables);
        builder.Services.AddSingleton(_play.Places);
        builder.Services.AddSingleton(_game.Texts);
        builder.Services.AddSingleton(new ClientBuild(TestInstall.Version, null));
        builder.Services.AddSingleton(new GameReferences(_play.World.Atlas, _play.Tables.Jobs, _play.Tables.Items, _play.World.Graph, monsters: _play.Tables.Monsters));
        builder.Services.AddSingleton<TimeProvider>(_play.Time);
        FightingModule.Register(builder.Services);
        builder.Services.AddSingleton(services => new ActivityCatalog(services.GetServices<ActivityDefinition>()));
        builder.Services.AddSingleton(new PageCatalog([]));
        StorageModule.Register(builder.Services, new StorageSettings { Path = _directory.Combine("d3b.db") });

        var host = builder.Build();

        Assert.Null(await host.Services.GetRequiredService<IHostPreparation>().PrepareAsync(TestContext.Current.CancellationToken));

        return host;
    }

    [Fact]
    public async Task AFightIsEditedItsMonstersChosenAndNoCharacterHasAStrategyInBothLanguagesAndThemes()
    {
        using var host = await HostAsync();
        var ct = TestContext.Current.CancellationToken;
        var profiles = host.Services.GetRequiredService<ProfileBook>();
        var accounts = host.Services.GetRequiredService<AccountBook>();

        await profiles.SaveAsync(0, "Larvae", FightingModule.ActivityId, """{"Mode":"Chosen","Place":"Chosen","SubAreas":[440]}""", ct);
        await accounts.SeeAsync(TestPlay.Account, [(TestPlay.CharacterId, "Character", 200)], _play.Time.GetUtcNow(), ct);

        var runner = await TestHosts.RunningAsync(host);
        var (englishTexts, fightTexts, accountTexts, picker) = await OnTheUIThreadAsync<(List<string>, List<string>, List<string>, bool)>(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 1800 };

            main.Refresh();
            window.Show();
            main.ShowProfilesCommand.Execute(null);
            main.Refresh();
            main.Profiles.Items.Single(profile => profile.Name == "Larvae").EditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var englishTexts = Texts(window);

            Drawings.Draw(window, "fight-en");
            main.Settings.UseFrenchCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var fightTexts = Texts(window);

            Drawings.Draw(window, "fight-fr");
            main.Profiles.Editing!.Editor!.Sections.SelectMany(section => section.Fields).OfType<PickedGamesFieldViewModel>().First(field => field.IsVisible).ChooseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var picker = window.GetVisualDescendants().OfType<GamePickerView>().Any(view => view.IsEffectivelyVisible);

            Drawings.Draw(window, "monsters-fr");
            main.Dialogs.Close();
            main.Profiles.Editing!.CloseCommand.Execute(null);
            main.ShowAccountsCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var accountTexts = Texts(window);

            Drawings.Draw(window, "accounts-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (englishTexts, fightTexts, accountTexts, picker);
        });

        Assert.Contains("Monsters to fight", englishTexts);
        Assert.True(picker);
        Assert.Contains("Monstres par groupe, au moins", fightTexts);
        Assert.Contains("Seulement les groupes gagnables", fightTexts);
        Assert.Contains("Character", accountTexts);
        Assert.DoesNotContain("Stratégie", accountTexts);
    }
}
