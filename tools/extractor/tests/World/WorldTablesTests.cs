using D3B.Extractor.Unity;
using D3B.Extractor.Texts;
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

    private static byte[] Fixture(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "world", name));
    }
}
