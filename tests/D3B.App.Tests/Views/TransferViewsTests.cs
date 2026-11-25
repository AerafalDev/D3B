using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Pages;
using D3B.Bot.Settings;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Transfers;
using D3B.Data;
using D3B.Data.Items;
using D3B.Data.Tests;
using D3B.Storage;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Tests.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

public sealed class TransferViewsTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();
    private readonly TemporaryDirectory _directory = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Concat(block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? [])).Where(text => text.Length > 0)];
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TransferViewsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
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
        builder.Services.AddSingleton(new GameReferences(_play.World.Atlas, _play.Tables.Jobs, _play.Tables.Items, _play.World.Graph));
        builder.Services.AddSingleton<TimeProvider>(_play.Time);
        TransfersModule.Register(builder.Services);
        builder.Services.AddSingleton(services => new ActivityCatalog(services.GetServices<ActivityDefinition>()));
        builder.Services.AddSingleton(new PageCatalog([]));
        StorageModule.Register(builder.Services, new StorageSettings { Path = _directory.Combine("d3b.db") });
        BehaviorsModule.Register(builder.Services, new StudioSettings { Path = _directory.Combine("studio") });

        var host = builder.Build();

        await TestHosts.PreparedAsync(host);

        return host;
    }

    [Fact]
    public async Task TheTransferProfileShowsWhichWayWhatAndHowManyInFrench()
    {
        using var host = await HostAsync();
        var definition = host.Services.GetRequiredService<ActivityCatalog>().All.Single();
        var settings = new TransferSettings { Categories = [ItemCategory.Resource], Items = [TestPlay.Bread], Quantity = TransferQuantity.KeepInSource, Amount = 50 };

        await host.Services.GetRequiredService<AssetLibrary>().SaveProfileAsync("Resources to the guild", TransfersModule.ActivityId, definition.Settings.Write(settings), TestContext.Current.CancellationToken);

        var runner = await TestHosts.RunningAsync(host);
        var french = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.French);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1280, Height = 1400 };

            main.Refresh();
            window.Show();
            main.Refresh();
            main.OpenProfile(main.Profiles.Items.Single(profile => profile.Name == "Resources to the guild").Id);
            Dispatcher.UIThread.RunJobs();

            var french = Texts(window);

            Drawings.Draw(window, "transfer-fr");
            localizer.Use(Localizer.English);
            window.Close();

            return french;
        });

        Assert.Contains("Quoi", french);
        Assert.Contains("De la banque au coffre de guilde", french);
        Assert.Contains("Ressources", french);
        Assert.Contains("Combien", french);
        Assert.Contains("En garder à la source", french);
        Assert.Contains("de chaque objet", french);
        Assert.Contains("Quand la destination refuse un objet", french);
        Assert.Contains("Le remettre à la source", french);
        Assert.Contains("Arrêter", french);
        Assert.Contains("Simulation", french);
    }
}
