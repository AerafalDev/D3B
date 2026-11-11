using D3B.Extractor.Code;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using D3B.Extractor.World;

namespace D3B.Extractor.Tests.World;

public sealed class WorldTablesTests
{
    private static readonly Text _astrub = new() { Fr = "Astrub", En = "Astrub", De = "Astrub", Es = "Astrub", Pt = "Astrub" };

    [Fact]
    public void AreasAreWrittenAsTheAppReadsThem()
    {
        Area[] areas =
        [
            new() { Id = 0, Name = _astrub, SuperAreaId = 0, WorldmapId = 0, HasWorldMap = false },
            new() { Id = 45, Name = null, SuperAreaId = 3, WorldmapId = 2, HasWorldMap = true },
        ];

        // The app's own tests read the same fixture (src-tauri/src/data/world.rs).
        Assert.Equal(Fixture("areas.json"), WorldTables.WriteAreas(areas));
    }

    [Fact]
    public void ZaapsAreWrittenAsTheAppReadsThem()
    {
        Zaap[] zaaps = [new() { Id = 1, MapId = 191106048, SubAreaId = 95, Activated = true }];

        Assert.Equal(Fixture("zaaps.json"), WorldTables.WriteZaaps(zaaps));
    }

    [Fact]
    public void PositionsAreWrittenAsTheAppReadsThem()
    {
        MapPosition[] positions =
        [
            new() { Id = 191106048, X = -1, Y = -27, SubAreaId = 95, WorldMap = 1, Name = null, Flags = ["capabilityAllowTeleportTo", "capabilityAllowTeleportFrom"] },
        ];

        Assert.Equal(Fixture("map-positions.json"), WorldTables.WritePositions(positions));
    }

    [Fact]
    public void SubAreasAreWrittenAsTheAppReadsThem()
    {
        SubArea[] subAreas =
        [
            new() { Id = 95, Name = _astrub, AreaId = 0, Level = 12, ZaapMapId = 191106048, BasicAccountAllowed = true, CustomWorldMapId = -1, Harvestables = [], Monsters = [31, 47] },
            new() { Id = 96, Name = null, AreaId = 0, Level = 1, ZaapMapId = null, BasicAccountAllowed = false, CustomWorldMapId = 3, Harvestables = [289], Monsters = [] },
        ];

        Assert.Equal(Fixture("sub-areas.json"), WorldTables.WriteSubAreas(subAreas));
    }

    [Fact]
    public void AnEmptyTableIsAnEmptyArray()
    {
        Assert.Equal("[]\n"u8.ToArray(), WorldTables.WriteZaaps([]));
    }

    [Fact]
    public void TheInstalledClientsWorldTablesRead()
    {
        var client = Installed.Client();
        var texts = TextFiles.Open(client);
        var areas = WorldTables.ReadAreas(DataTable.ReadById(client, "areas"), texts);
        var zaaps = WorldTables.ReadZaaps(DataTable.ReadById(client, "waypoints"));

        Assert.NotEmpty(areas);
        Assert.Equal(areas.Select(area => area.Id).Order(), areas.Select(area => area.Id));
        Assert.NotEmpty(zaaps);
    }

    [Fact]
    public void TheInstalledClientsPositionsAndSubAreasRead()
    {
        var client = Installed.Client();
        var texts = TextFiles.Open(client);

        using var code = ClientCode.Open(client);

        var positions = WorldTables.ReadPositions(DataTable.Read(client, "mapsinformation"), texts, code.FlagEnum("MapInformationFlags"));
        var subAreas = WorldTables.ReadSubAreas(DataTable.ReadById(client, "subareas"), texts);

        Assert.NotEmpty(positions);
        Assert.Contains(positions, position => position.Flags.Contains("capabilityAllowTeleportTo"));
        Assert.Contains(subAreas, subArea => subArea.ZaapMapId is not null);
    }

    private static byte[] Fixture(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "world", name));
    }
}
