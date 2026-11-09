using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Settings;
using D3B.Data.Tests;
using D3B.Data.World;
using D3B.Studio.Assets;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels;

/// <summary>The field that picks the zone a harvest or a hunt goes through, among the project's zones (D415).</summary>
[UiThread]
public sealed class ZoneFieldTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly AssetLibrary _library;
    private readonly Dialogs _dialogs = new();

    public ZoneFieldTests()
    {
        _library = new AssetLibrary(new StudioSettings { Path = _directory.Combine("studio") });
    }

    public void Dispose()
    {
        _library.Dispose();
        _directory.Dispose();
    }

    // Maps 1 and 2 lie in sub-area 10, map 3 in sub-area 20.
    private static WorldAtlas Atlas()
    {
        MapPosition[] positions = [new(1, 1, 0, 10, 1, 0, 0), new(2, 2, 0, 10, 1, 0, 0), new(3, 3, 0, 20, 1, 0, 0)];
        SubArea[] subAreas = [new(10, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0), new(20, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0)];

        return new WorldAtlas(positions, subAreas, new[] { new Area(1, 0, 0) }, Array.Empty<Waypoint>(), Array.Empty<int>(), Array.Empty<int>());
    }

    private async Task<(SettingsEditor Editor, ZoneFieldViewModel Field, HarvestSettings Draft)> EditAsync(HarvestSettings draft, params ZoneAsset[] zones)
    {
        foreach (var zone in zones)
        {
            var file = _directory.Combine("studio", zone.Name + ".zone.json");

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, AssetFiles.Write(zone), TestContext.Current.CancellationToken);
        }

        await _library.LoadAsync([], TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        var atlas = Atlas();

        services.AddSingleton(_library);
        services.AddSingleton<IPlaceZones>(new ProjectZones(_library, () => atlas));

        var harvest = new ServiceCollection();

        HarvestingModule.Register(harvest);

        var definition = Assert.Single(harvest.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
        var localizer = new Localizer(Localizer.English);
        var game = new GameReferences(TestReferences.Atlas(), TestReferences.Jobs(), TestReferences.Items());
        var editor = new SettingsEditor(definition.Settings, draft, game, new GameNames(null, localizer), localizer, services.BuildServiceProvider(), dialogs: _dialogs);

        return (editor, Assert.IsType<ZoneFieldViewModel>(editor.FieldOf("Zone")), draft);
    }

    [Fact]
    public async Task TheFieldShowsOnlyWhenThePlaceIsAZoneAndSaysWhatAZoneDoesUntilOneIsPicked()
    {
        var (editor, field, draft) = await EditAsync(new HarvestSettings { Resources = [TestReferences.Ash], SubAreas = [10] });

        Assert.False(field.IsVisible);

        draft.Place = HarvestPlace.Zone;
        editor.Changed(editor.FieldOf("Place")!);

        Assert.True(field.IsVisible);
        Assert.Equal("choose a zone", field.Text);
        Assert.False(field.HasValue);
        Assert.StartsWith("The character harvests only on the maps of this zone", field.Detail);
    }

    [Fact]
    public async Task PickingAZoneNamesItAndSaysHowManyMapsItHolds()
    {
        var (editor, field, draft) = await EditAsync(new HarvestSettings { Resources = [TestReferences.Ash], Place = HarvestPlace.Zone }, new ZoneAsset { Id = "z1", Name = "North", SubAreas = [10] }, new ZoneAsset { Id = "z2", Name = "Cave", Maps = [3] });

        field.PickCommand.Execute(null);

        var picker = Assert.IsType<RoutePickerViewModel>(_dialogs.Current);

        Assert.Equal(["Cave", "North"], picker.Routes.Select(zone => zone.Name));
        Assert.Equal(["1 map", "2 maps"], picker.Routes.Select(zone => zone.Detail));
        Assert.False(picker.CanClear);
        picker.Routes[1].ChooseCommand.Execute(null);

        Assert.Equal("z1", draft.Zone);
        Assert.Equal("North", field.Text);
        Assert.StartsWith("2 maps", field.Detail);
        Assert.False(editor.HasProblems);

        field.ClearCommand.Execute(null);

        Assert.Equal("", draft.Zone);
        Assert.True(editor.HasProblems);
    }

    [Fact]
    public async Task AZoneThatHoldsNoMapOrIsGoneBlocksTheProfileAndSaysWhy()
    {
        var (editor, field, draft) = await EditAsync(new HarvestSettings { Resources = [TestReferences.Ash], Place = HarvestPlace.Zone, Zone = "z1" }, new ZoneAsset { Id = "z1", Name = "Empty" });

        Assert.True(editor.HasProblems);
        Assert.Equal("This zone holds no map yet.", field.Problem);
        Assert.Equal("Empty", field.Text);

        draft.Zone = "z9";
        editor.Changed(field);

        Assert.Equal("This zone is not in the project.", field.Problem);
        Assert.Equal("z9 (missing)", field.Text);
        Assert.True(editor.HasProblems);
    }
}
