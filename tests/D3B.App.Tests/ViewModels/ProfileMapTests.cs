using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Data.Tests;
using D3B.Studio.Assets;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels;

/// <summary>The world's map beside a profile: the project's zones tinted, the chosen one outlined, and the place picked on it (D415).</summary>
[UiThread]
public sealed class ProfileMapTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly AssetLibrary _library;
    private readonly List<string> _edited = [];
    private RouteServices? _services;

    public ProfileMapTests()
    {
        _library = new AssetLibrary(new StudioSettings { Path = _directory.Combine("studio") });
    }

    public void Dispose()
    {
        _library.Dispose();
        _directory.Dispose();
    }

    private async Task<(ProfileMapViewModel Map, SettingsEditor Editor, HarvestSettings Draft)> OpenAsync(HarvestSettings draft, bool runs = true, params ZoneAsset[] zones)
    {
        foreach (var zone in zones)
        {
            var file = _directory.Combine("studio", zone.Name + ".zone.json");

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, AssetFiles.Write(zone), TestContext.Current.CancellationToken);
        }

        await _library.LoadAsync([], TestContext.Current.CancellationToken);

        _services = runs ? TestRouteWorld.Services(_library) : null;

        var harvest = new ServiceCollection();

        HarvestingModule.Register(harvest);

        var definition = Assert.Single(harvest.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
        var localizer = new Localizer(Localizer.English);
        var provided = new ServiceCollection();

        provided.AddSingleton(_library);

        if (_services is not null)
            provided.AddSingleton<IPlaceZones>(new ProjectZones(_library, () => _services.Atlas));

        var editor = new SettingsEditor(definition.Settings, draft, null, new GameNames(null, localizer), localizer, provided.BuildServiceProvider());
        var map = new ProfileMapViewModel(localizer, () => _services, _edited.Add);

        map.Use(editor);

        return (map, editor, draft);
    }

    private static string[] Ids(ProfileMapViewModel map)
    {
        return [.. map.Scene.Backdrop!.Zones.Select(zone => zone.Id).Order()];
    }

    [Fact]
    public async Task TheMapTintsTheProjectsZonesAndOutlinesTheOneChosen()
    {
        var (map, _, _) = await OpenAsync(
            new HarvestSettings { Place = HarvestPlace.Zone, Zone = "a" },
            true,
            new ZoneAsset { Id = "a", Name = "Ash", Maps = [1, 2] },
            new ZoneAsset { Id = "b", Name = "Birch", Maps = [4] });

        Assert.True(map.IsAvailable);
        Assert.Equal(1, map.Layer);
        Assert.Equal(["a", "b"], Ids(map));
        Assert.Equal([true, false], map.Scene.Backdrop!.Zones.OrderBy(zone => zone.Id).Select(zone => zone.IsSelected));
        Assert.Equal([(1, 0), (2, 0)], map.Scene.Squares!.Select(square => (square.X, square.Y)).Order());
        Assert.All(map.Scene.Squares!, square => Assert.False(square.IsLeftOut));
    }

    [Fact]
    public async Task AClickOnAZonePicksItWhenThePlaceIsAZoneAndTheMapFollows()
    {
        var (map, editor, draft) = await OpenAsync(
            new HarvestSettings { Place = HarvestPlace.Zone },
            true,
            new ZoneAsset { Id = "a", Name = "Ash", Maps = [1, 2] },
            new ZoneAsset { Id = "b", Name = "Birch", Maps = [4] });

        Assert.Empty(map.Scene.Squares ?? []);

        map.Clicked(new WorldMapHit(WorldMapHitKind.Zone, 4, 0, -1, -1, "b"));

        Assert.Equal("b", draft.Zone);
        Assert.Equal("Birch", Assert.IsType<ZoneFieldViewModel>(editor.FieldOf("Zone")).Text);
        Assert.Equal([(4, 0)], map.Scene.Squares!.Select(square => (square.X, square.Y)));
        Assert.True(map.Scene.Backdrop!.Zones.Single(zone => zone.Id == "b").IsSelected);
    }

    [Fact]
    public async Task AClickOnTheSeaOrWhenThePlaceIsAutomaticChangesNothing()
    {
        var (map, editor, draft) = await OpenAsync(new HarvestSettings { Place = HarvestPlace.Zone, Zone = "a" }, true, new ZoneAsset { Id = "a", Name = "Ash", Maps = [1] });

        map.Clicked(new WorldMapHit(WorldMapHitKind.Square, 3, 0, -1, -1));
        map.Clicked(WorldMapHit.None);

        Assert.Equal("a", draft.Zone);

        draft.Place = HarvestPlace.Automatic;
        editor.Changed();
        map.Clicked(new WorldMapHit(WorldMapHitKind.Zone, 1, 0, -1, -1, "a"));

        Assert.Equal("a", draft.Zone);
        Assert.Empty(draft.SubAreas);
        Assert.Equal("The bot chooses the place: pick Zone or Sub-areas to point it out on the map.", map.Hint);
    }

    [Fact]
    public async Task AClickOnAMapAddsItsSubAreaAndAnotherClickTakesItOutWhenThePlaceIsSubAreas()
    {
        var (map, _, draft) = await OpenAsync(new HarvestSettings { Place = HarvestPlace.Chosen }, true, new ZoneAsset { Id = "a", Name = "Ash", Maps = [1] });

        Assert.Equal("Click a sub-area to add it or take it out.", map.Hint);

        // Map 9 lies inside, at the coordinates of map 2: the sub-area is the one of the map on the world, not the vault's.
        map.Clicked(new WorldMapHit(WorldMapHitKind.Square, 2, 0, -1, -1));

        Assert.Equal([TestRouteWorld.SubAreaId], draft.SubAreas);
        Assert.Equal([(1, 0), (2, 0), (3, 0), (4, 0), (5, 0)], map.Scene.Squares!.Select(square => (square.X, square.Y)).Order());

        map.Clicked(new WorldMapHit(WorldMapHitKind.Zone, 1, 0, -1, -1, "a"));

        Assert.Empty(draft.SubAreas);
        Assert.Empty(map.Scene.Squares ?? []);
    }

    [Fact]
    public async Task TheMapOffersToEditTheChosenZoneInTheWorld()
    {
        var (map, editor, draft) = await OpenAsync(new HarvestSettings { Place = HarvestPlace.Chosen }, true, new ZoneAsset { Id = "a", Name = "Ash", Maps = [1] });

        Assert.False(map.CanEditZone);

        draft.Place = HarvestPlace.Zone;
        draft.Zone = "a";
        editor.Changed();

        Assert.True(map.CanEditZone);

        map.EditZoneCommand.Execute(null);

        Assert.Equal(["a"], _edited);

        // A zone gone from the project has nothing to edit.
        draft.Zone = "gone";
        editor.Changed();

        Assert.False(map.CanEditZone);
    }

    [Fact]
    public async Task TheMapNamesWhatLiesUnderThePointerAndHasNothingToShowBeforeTheHostRuns()
    {
        var (map, _, _) = await OpenAsync(new HarvestSettings { Place = HarvestPlace.Zone }, true, new ZoneAsset { Id = "a", Name = "Ash", Maps = [1] });

        map.Hover(new WorldMapHit(WorldMapHitKind.Zone, 1, 0, -1, -1, "a"));

        Assert.Equal("Ash", map.Hovered);

        map.Hover(WorldMapHit.None);

        Assert.Equal("", map.Hovered);

        var (none, _, _) = await OpenAsync(new HarvestSettings { Place = HarvestPlace.Zone }, false);

        Assert.False(none.IsAvailable);
        Assert.Null(none.Source);
        Assert.Equal(WorldMapScene.Empty, none.Scene);
    }

    [Fact]
    public async Task ThePickedZoneOfAnotherWorldMakesTheMapShowThatWorld()
    {
        var (map, _, _) = await OpenAsync(new HarvestSettings { Place = HarvestPlace.Zone, Zone = "far" }, true, new ZoneAsset { Id = "far", Name = "Far", Maps = [7] }, new ZoneAsset { Id = "near", Name = "Near", Maps = [1] });

        Assert.Equal(2, map.Layer);
        Assert.Equal(["far"], Ids(map));
    }
}
