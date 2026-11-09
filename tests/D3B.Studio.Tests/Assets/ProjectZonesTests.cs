using D3B.Data.Tests;
using D3B.Data.World;
using D3B.Studio.Assets;

namespace D3B.Studio.Tests.Assets;

public sealed class ProjectZonesTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly AssetLibrary _library;
    private int _atlasReads;

    public ProjectZonesTests()
    {
        _library = new AssetLibrary(new StudioSettings { Path = _directory.Combine("studio") });
    }

    public void Dispose()
    {
        _library.Dispose();
        _directory.Dispose();
    }

    // Maps 1 and 2 lie in sub-area 10, map 3 in sub-area 20.
    private WorldAtlas Atlas()
    {
        _atlasReads++;

        MapPosition[] positions = [new(1, 1, 0, 10, 1, 0, 0), new(2, 2, 0, 10, 1, 0, 0), new(3, 3, 0, 20, 1, 0, 0)];
        SubArea[] subAreas = [new(10, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0), new(20, 0, 1, 10, SubArea.NoZaap, true, 0, 0, 0, 0)];

        return new WorldAtlas(positions, subAreas, new[] { new Area(1, 0, 0) }, Array.Empty<Waypoint>(), Array.Empty<int>(), Array.Empty<int>());
    }

    private void WriteZone(ZoneAsset zone)
    {
        var file = _directory.Combine("studio", zone.Name + ".zone.json");

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, AssetFiles.Write(zone));
    }

    [Fact]
    public async Task AZoneOfTheProjectGivesItsMapsAndOneThatIsNotThereIsSaidSo()
    {
        WriteZone(new ZoneAsset { Id = "z1", Name = "North", SubAreas = [10], Maps = [3], Excluded = [2] });
        WriteZone(new ZoneAsset { Id = "z2", Name = "Nothing" });
        await _library.LoadAsync([], TestContext.Current.CancellationToken);

        var zones = new ProjectZones(_library, Atlas);

        Assert.True(zones.TryResolve("z1", out var maps));
        Assert.Equal([1L, 3L], maps.Order());
        Assert.True(zones.TryResolve("z2", out var none));
        Assert.Empty(none);
        Assert.False(zones.TryResolve("z9", out var gone));
        Assert.Empty(gone);
        Assert.False(zones.TryResolve("", out _));
    }

    [Fact]
    public async Task ZonesAreReckonedOncePerVersionOfTheProjectAndAgainOnceItChanges()
    {
        WriteZone(new ZoneAsset { Id = "z1", Name = "North", SubAreas = [10] });
        await _library.LoadAsync([], TestContext.Current.CancellationToken);

        var zones = new ProjectZones(_library, Atlas);

        zones.TryResolve("z1", out _);
        zones.TryResolve("z1", out _);

        Assert.Equal(1, _atlasReads);

        await _library.SaveAsync(new ZoneAsset { Id = "z1", Name = "North", SubAreas = [20] }, TestContext.Current.CancellationToken);

        Assert.True(zones.TryResolve("z1", out var maps));
        Assert.Equal([3L], maps);
        Assert.Equal(2, _atlasReads);
    }
}
