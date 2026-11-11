using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.World;

/// <summary>The world's tables of the data folder: its areas and zaaps (docs/data.md).</summary>
internal static class WorldTables
{
    /// <summary>Reads the world's tables from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">The client's tables are not what the extractor measured.</exception>
    public static void Extract(ClientInstall client, TextFiles texts, DataFolder folder, TextWriter output)
    {
        var areas = ReadAreas(DataTable.ReadById(client, "areas"), texts);
        var zaaps = ReadZaaps(DataTable.ReadById(client, "waypoints"));

        folder.Write("areas.json", WriteAreas(areas));
        folder.Write("zaaps.json", WriteZaaps(zaaps));

        output.WriteLine($"World: {areas.Count:N0} areas, {zaaps.Count:N0} zaaps");
    }

    public static IReadOnlyList<Area> ReadAreas(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record => new Area
                {
                    Id = record.Int32("id"),
                    Name = texts.Name(record.Int32("nameId")),
                    SuperAreaId = record.Int32("superAreaId"),
                    WorldmapId = record.Int32("worldmapId"),
                    HasWorldMap = record.Boolean("hasWorldMap"),
                })
                .OrderBy(area => area.Id),
        ];
    }

    /// <exception cref="InvalidDataException">A zaap stands on no map.</exception>
    public static IReadOnlyList<Zaap> ReadZaaps(IEnumerable<Fields> records)
    {
        var zaaps = records
            .Select(record => new Zaap
            {
                Id = record.Int32("id"),
                MapId = record.Int32("mapId"),
                SubAreaId = record.Int32("subAreaId"),
                Activated = record.Boolean("activated"),
            })
            .OrderBy(zaap => zaap.Id)
            .ToList();

        foreach (var zaap in zaaps)
        {
            if (zaap.MapId <= 0)
            {
                throw new InvalidDataException($"Zaap {zaap.Id} stands on map {zaap.MapId}, which is no map.");
            }
        }

        return zaaps;
    }

    public static byte[] WriteAreas(IEnumerable<Area> areas)
    {
        return JsonTable.Write(areas, (json, area) =>
        {
            json.WriteNumber("id", area.Id);
            JsonTable.WriteText(json, "name", area.Name);
            json.WriteNumber("superAreaId", area.SuperAreaId);
            json.WriteNumber("worldmapId", area.WorldmapId);
            json.WriteBoolean("hasWorldMap", area.HasWorldMap);
        });
    }

    public static byte[] WriteZaaps(IEnumerable<Zaap> zaaps)
    {
        return JsonTable.Write(zaaps, (json, zaap) =>
        {
            json.WriteNumber("id", zaap.Id);
            json.WriteNumber("mapId", zaap.MapId);
            json.WriteNumber("subAreaId", zaap.SubAreaId);
            json.WriteBoolean("activated", zaap.Activated);
        });
    }
}
