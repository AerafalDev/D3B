using D3B.Extractor.Code;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.World;

/// <summary>The world's tables of the data folder: its map positions, sub-areas, areas and zaaps (docs/data.md).</summary>
internal static class WorldTables
{
    /// <summary>Reads the world's tables from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">The client's tables are not what the extractor measured.</exception>
    public static void Extract(ClientInstall client, TextFiles texts, ClientCode code, DataFolder folder, TextWriter output)
    {
        var positions = ReadPositions(DataTable.Read(client, "mapsinformation"), texts, code.FlagEnum("MapInformationFlags"));
        var subAreas = ReadSubAreas(DataTable.ReadById(client, "subareas"), texts);
        var areas = ReadAreas(DataTable.ReadById(client, "areas"), texts);
        var zaaps = ReadZaaps(DataTable.ReadById(client, "waypoints"));

        folder.Write("map-positions.json", WritePositions(positions));
        folder.Write("sub-areas.json", WriteSubAreas(subAreas));
        folder.Write("areas.json", WriteAreas(areas));
        folder.Write("zaaps.json", WriteZaaps(zaaps));

        output.WriteLine($"World: {positions.Count:N0} map positions, {subAreas.Count:N0} sub-areas, {areas.Count:N0} areas, {zaaps.Count:N0} zaaps");
    }

    /// <exception cref="InvalidDataException">A position is keyed under another map's id, or sets a flag its enum does not name.</exception>
    public static IReadOnlyList<MapPosition> ReadPositions(IEnumerable<(long Key, Fields Record)> rows, TextFiles texts, FlagEnum flags)
    {
        var positions = new List<MapPosition>();

        foreach (var (key, record) in rows)
        {
            var position = new MapPosition
            {
                Id = record.UInt32("id"),
                X = record.Int16("posX"),
                Y = record.Int16("posY"),
                SubAreaId = record.UInt16("subAreaId"),
                WorldMap = record.SByte("worldMap"),
                Name = texts.Name(record.Int32("nameId")),
                Flags = flags.Names(record.UInt32("m_flags")),
            };

            if (key != position.Id)
            {
                throw new InvalidDataException($"The maps' information keys map {position.Id} as {key}.");
            }

            positions.Add(position);
        }

        return [.. positions.OrderBy(position => position.Id)];
    }

    /// <exception cref="InvalidDataException">A sub-area names a zaap map that is no map.</exception>
    public static IReadOnlyList<SubArea> ReadSubAreas(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record => new SubArea
                {
                    Id = record.Int32("id"),
                    Name = texts.Name(record.Int32("nameId")),
                    AreaId = record.Int32("areaId"),
                    Level = record.UInt32("level"),
                    ZaapMapId = ZaapOf(record.Int32("id"), record.Int32("associatedZaapMapId")),
                    BasicAccountAllowed = record.Boolean("basicAccountAllowed"),
                    CustomWorldMapId = record.Int32("customWorldMapId"),
                    Harvestables = record.Int32Array("harvestables"),
                    Monsters = record.Int32Array("monsters"),
                })
                .OrderBy(subArea => subArea.Id),
        ];
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

    public static byte[] WritePositions(IEnumerable<MapPosition> positions)
    {
        return JsonTable.Write(positions, (json, position) =>
        {
            json.WriteNumber("id", position.Id);
            json.WriteNumber("x", position.X);
            json.WriteNumber("y", position.Y);
            json.WriteNumber("subAreaId", position.SubAreaId);
            json.WriteNumber("worldMap", position.WorldMap);
            JsonTable.WriteText(json, "name", position.Name);
            json.WriteStartArray("flags");

            foreach (var flag in position.Flags)
            {
                json.WriteStringValue(flag);
            }

            json.WriteEndArray();
        });
    }

    public static byte[] WriteSubAreas(IEnumerable<SubArea> subAreas)
    {
        return JsonTable.Write(subAreas, (json, subArea) =>
        {
            json.WriteNumber("id", subArea.Id);
            JsonTable.WriteText(json, "name", subArea.Name);
            json.WriteNumber("areaId", subArea.AreaId);
            json.WriteNumber("level", subArea.Level);

            if (subArea.ZaapMapId is { } zaap)
            {
                json.WriteNumber("zaapMapId", zaap);
            }
            else
            {
                json.WriteNull("zaapMapId");
            }

            json.WriteBoolean("basicAccountAllowed", subArea.BasicAccountAllowed);
            json.WriteNumber("customWorldMapId", subArea.CustomWorldMapId);
            WriteNumbers(json, "harvestables", subArea.Harvestables);
            WriteNumbers(json, "monsters", subArea.Monsters);
        });
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

    private static int? ZaapOf(int subAreaId, int mapId)
    {
        // Every sub-area without a zaap names map 0 (3.7.12.12, main:docs/client.md); another value that is no map is not measured.
        return mapId switch
        {
            > 0 => mapId,
            0 => null,
            _ => throw new InvalidDataException($"Sub-area {subAreaId} names zaap map {mapId}."),
        };
    }

    private static void WriteNumbers(System.Text.Json.Utf8JsonWriter json, string name, int[] numbers)
    {
        json.WriteStartArray(name);

        foreach (var number in numbers)
        {
            json.WriteNumberValue(number);
        }

        json.WriteEndArray();
    }
}
