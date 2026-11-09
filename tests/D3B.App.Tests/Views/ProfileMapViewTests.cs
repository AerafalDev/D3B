using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Pages;
using D3B.Bot.Settings;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Data;
using D3B.Data.Tests;
using D3B.Storage;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Tests.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

public sealed class ProfileMapViewTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();
    private readonly TemporaryDirectory _directory = new();

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ProfileMapViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
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
        HarvestingModule.Register(builder.Services);
        builder.Services.AddSingleton(services => new ActivityCatalog(services.GetServices<ActivityDefinition>()));
        builder.Services.AddSingleton(new PageCatalog([]));
        StorageModule.Register(builder.Services, new StorageSettings { Path = _directory.Combine("d3b.db") });
        BehaviorsModule.Register(builder.Services, new StudioSettings { Path = _directory.Combine("studio") });

        var host = builder.Build();

        await TestHosts.PreparedAsync(host);

        return host;
    }

    [Fact]
    public async Task TheMapBesideAProfilePicksTheZoneOnAClickAndLeavesTheColumnForABandInANarrowTab()
    {
        using var host = await HostAsync();
        var ct = TestContext.Current.CancellationToken;
        var definition = host.Services.GetRequiredService<ActivityCatalog>().All.Single();
        var assets = host.Services.GetRequiredService<AssetLibrary>();

        await assets.SaveAsync(new ZoneAsset { Id = "ash", Name = "Ash wood", Maps = [1, 2] }, ct);
        await assets.SaveAsync(new ZoneAsset { Id = "birch", Name = "Birch wood", Maps = [4] }, ct);
        await assets.SaveProfileAsync("In a zone", HarvestingModule.ActivityId, definition.Settings.Write(new HarvestSettings { Resources = [TestPlay.Ash], Place = HarvestPlace.Zone, Zone = "ash" }), ct);

        var runner = await TestHosts.RunningAsync(host);
        var result = await OnTheUIThreadAsync(() =>
        {
            var localizer = Localizer.Instance;

            localizer.Use(Localizer.English);

            var main = new MainViewModel(runner, localizer);
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };

            main.Refresh();
            main.Routes.Use(TestRouteWorld.Services(assets));
            window.Show();
            main.Refresh();
            main.OpenProfile(main.Profiles.Items.Single(profile => profile.Name == "In a zone").Id);
            main.Refresh();
            Dispatcher.UIThread.RunJobs();

            var view = window.GetVisualDescendants().OfType<ProfileView>().Single(item => item.IsEffectivelyVisible);
            var map = view.GetVisualDescendants().OfType<WorldMapView>().Single();
            var wide = (view.Classes.Contains("Band"), map.GetVisualAncestors().Any(parent => parent is Border { Name: "WideHost" }), Math.Round(map.Bounds.Width));
            var picked = main.Profiles.Editing!.Editor!.FieldOf("Zone") as ZoneFieldViewModel;
            var before = picked!.Value;

            Drawings.Draw(window, "profile-map-wide-en");

            // A click on a map of the other zone picks that zone: the field says it, the map follows.
            var at = map.TranslatePoint(map.RectOf(4, 0).Center, window)!.Value;

            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var after = (picked.Value, picked.Text, Squares: map.Scene.Squares!.Select(square => (square.X, square.Y)).ToList());

            // A tab too narrow for the column puts the map above the form.
            window.Width = 960;
            Dispatcher.UIThread.RunJobs();

            var band = (view.Classes.Contains("Band"), map.GetVisualAncestors().Any(parent => parent is Border { Name: "BandHost" }));

            Drawings.Draw(window, "profile-map-band-en");
            window.Width = 1440;
            Dispatcher.UIThread.RunJobs();

            var back = (view.Classes.Contains("Band"), map.GetVisualAncestors().Any(parent => parent is Border { Name: "WideHost" }));

            window.Close();

            return (wide, before, after, band, back);
        });

        Assert.False(result.wide.Item1);
        Assert.True(result.wide.Item2);
        Assert.InRange(result.wide.Item3, 250, 430);
        Assert.Equal("ash", result.before);
        Assert.Equal(("birch", "Birch wood"), (result.after.Value, result.after.Text));
        Assert.Equal([(4, 0)], result.after.Item3);
        Assert.Equal((true, true), result.band);
        Assert.Equal((false, true), result.back);
    }
}
