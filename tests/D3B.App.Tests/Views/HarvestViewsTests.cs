using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Hosting;
using D3B.Bot.Pages;
using D3B.Bot.Settings;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Data;
using D3B.Data.Tests;
using D3B.Storage;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

public sealed class HarvestViewsTests : IAsyncDisposable
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
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HarvestViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static void Draw(Window window, string name)
    {
        // Both themes drawn, as the app has them; the app's own is given back.
        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            Application.Current!.RequestedThemeVariant = theme;
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(window.CaptureRenderedFrame());
            Drawings.Save(window, $"{name}-{theme}");
        }

        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();
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
        builder.Services.AddSingleton(_game.Texts);
        builder.Services.AddSingleton(new ClientBuild(TestInstall.Version, null));
        builder.Services.AddSingleton(new GameReferences(_play.World.Atlas, _play.Tables.Jobs, _play.Tables.Items, _play.World.Graph));
        builder.Services.AddSingleton<TimeProvider>(_play.Time);
        HarvestingModule.Register(builder.Services);
        builder.Services.AddSingleton(services => new ActivityCatalog(services.GetServices<ActivityDefinition>()));
        builder.Services.AddSingleton(new PageCatalog([]));
        StorageModule.Register(builder.Services, new StorageSettings { Path = _directory.Combine("d3b.db") });

        var host = builder.Build();

        Assert.Null(await host.Services.GetRequiredService<IHostPreparation>().PrepareAsync(TestContext.Current.CancellationToken));

        return host;
    }

    [Fact]
    public async Task TheHarvestProfileShowsItsResourcesAsTilesItsZoneAsAMapAndItsFiguresInBothLanguagesAndThemes()
    {
        using var host = await HostAsync();
        var definition = host.Services.GetRequiredService<ActivityCatalog>().All.Single();
        var settings = new HarvestSettings { Resources = [TestPlay.Ash, TestPlay.Chestnut], SubAreas = [TestPlay.SubAreaId] };

        await host.Services.GetRequiredService<ProfileBook>().SaveAsync(0, "Ash and chestnut", HarvestingModule.ActivityId, definition.Settings.Write(settings), TestContext.Current.CancellationToken);

        var runner = await TestHosts.RunningAsync(host);
        var (english, french, zone) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 1400 };

            main.Refresh();
            window.Show();
            main.ShowProfilesCommand.Execute(null);
            main.Refresh();
            main.Profiles.Items[0].EditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var english = Texts(window);
            var zone = window.GetVisualDescendants().OfType<ZoneMapCanvas>().Single().Scene;

            Draw(window, "profile-en");
            main.SwitchLanguageCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            var french = Texts(window);

            Draw(window, "profile-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, zone);
        });

        Assert.Contains("What it harvests", english);
        Assert.Contains("Ash", english);
        Assert.Contains("Chestnut", english);
        Assert.Contains("Lumberjack, level 1", english);
        Assert.Contains("Lumberjack, level 20", english);
        Assert.Contains("Where your resources grow", english);
        Assert.Contains("Level 10, 5 maps, Ash, Chestnut", english);
        Assert.Contains("maps to go through", english);
        Assert.Contains("2 of 2", english);
        Assert.Contains("1 map", english);
        Assert.Contains("to the nearest bank", english);
        Assert.Contains("90%", english);
        Assert.Contains("What harvesting brings", english);
        Assert.Contains("Everything but what is worn", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Ce qu'il récolte", french);
        Assert.Contains("Frêne", french);
        Assert.Contains("Bûcheron, niveau 20", french);
        Assert.Contains("Où poussent tes ressources", french);
        Assert.Contains("cartes à parcourir", french);
        Assert.Contains("2 sur 2", french);
        Assert.Contains("90 %", french);
        Assert.Contains("Ce que la récolte rapporte", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));

        // The zone's three maps of the world map, the insides on their coordinates, and the bank in front of one.
        Assert.NotNull(zone);
        Assert.Equal(3, zone.Squares.Count(square => square.Kind is ZoneSquareKind.GroupA));
        Assert.Equal(new PixelPoint(4, -18), zone.Landmark);
    }

    [Fact]
    public async Task TheCardAndTheMapSayWhatTheHarvestDoesInBothLanguagesAndThemes()
    {
        using var host = await HostAsync();
        var definition = host.Services.GetRequiredService<ActivityCatalog>().All.Single();
        var runner = await TestHosts.RunningAsync(host);
        var (_, bot) = await _play.EnterAsync(433);
        var run = bot.StartActivity(definition.Create(host.Services, new HarvestSettings { Resources = [TestPlay.Ash, TestPlay.Chestnut], SubAreas = [TestPlay.SubAreaId] }));

        await Waiting.UntilAsync(() => run.Activity.Report?.Focus?.TargetElementId is not null, TimeSpan.FromSeconds(10));

        var (english, french, map) = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 900 };
            var english = new List<string>();
            var french = new List<string>();

            main.Refresh();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            english.AddRange(Texts(window));
            Draw(window, "card-en");
            main.Bots.Items[0].ShowMapCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            english.AddRange(Texts(window));
            Draw(window, "map-en");

            var map = (main.Map.Scene?.Activity?.Target, main.Map.Scene?.Activity?.Wanted.ToArray(), main.Map.Tour?.Squares.Length);

            main.SwitchLanguageCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            french.AddRange(Texts(window));
            main.ShowBotsCommand.Execute(null);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();
            french.AddRange(Texts(window));
            Draw(window, "card-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return (english, french, map);
        });

        run.Stop();
        await run.Completion;

        Assert.Contains("Harvesting", english);
        Assert.Contains("Harvesting Ash", english);
        Assert.Contains("Tour 1, map 1 of 5", english);
        Assert.Contains("Ash 0", english);
        Assert.Contains("Chestnut needs Lumberjack 20, the character is 1", english);
        Assert.Contains("Activity", english);
        Assert.Contains("The tour", english);
        Assert.Contains("Harvesting Ash · Tour 1, map 1 of 5", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Récolte : Frêne", french);
        Assert.Contains("Tournée 1, carte 1 sur 5", french);
        Assert.Contains("Frêne 0", french);
        Assert.Contains("Châtaignier demande Bûcheron 20, le perso est 1", french);
        Assert.Contains("Activité", french);
        Assert.Contains("La tournée", french);
        Assert.DoesNotContain(french, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Equal(TestPlay.TreeCell, map.Target);
        Assert.Equal([TestPlay.TreeCell], map.Item2);
        Assert.True(map.Item3 > 0);
    }
}
