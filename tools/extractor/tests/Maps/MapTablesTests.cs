using D3B.Extractor.Maps;

namespace D3B.Extractor.Tests.Maps;

public sealed class MapTablesTests
{
    [Fact]
    public void MapsAreWrittenAsTheAppReadsThem()
    {
        var flags = new ushort[MapData.CellCount];
        var zones = new byte[MapData.CellCount];

        flags[0] = 1 | 2 | 32;
        zones[0] = 0x10;

        MapData[] maps =
        [
            new()
            {
                Id = 191106007, Top = 191106008, Bottom = 191106006, Left = 0, Right = 191106520, Flags = flags, LinkedZone = zones,
                Numbers = new Dictionary<string, SortedDictionary<int, int>> { ["mapChange"] = new() { [0] = 131 }, ["floor"] = new() { [12] = -20 } },
                Elements = [(465453, 300, 7506)],
            },
        ];
        var expected = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "maps", "7.json"));

        // The app's own tests read the same fixture (src-tauri/src/data/maps.rs).
        Assert.Equal(expected, MapTables.Write(maps));
        Assert.Equal("maps/7.json", MapTables.PathOf(191106007 % MapTables.Shards));
    }

    [Fact]
    public void TheBitsOfACellAreItsBooleansInOrder()
    {
        Assert.Equal(["mov", "los"], CellFlags.Names.Take(2));
        Assert.Equal(10, CellFlags.Names.Count);
    }

    [Fact]
    public void TheInstalledClientsMapsHaveTheirCellsAndAnId()
    {
        var client = Installed.Client();
        using var temporary = new TemporaryFolder();
        var folder = DataFolder.Create(Path.Combine(temporary.Path, "data"));
        using var output = new StringWriter();

        var maps = MapTables.Extract(client, folder, output);

        Assert.NotEmpty(maps);
        Assert.All(maps, map => Assert.Equal(MapData.CellCount, map.Flags.Length));
        Assert.Equal(maps.Count, maps.Select(map => map.Id).Distinct().Count());
        Assert.Contains(maps, map => map.Elements.Count > 0);
    }
}
