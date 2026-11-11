using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.Unity;

public sealed class DataTableTests
{
    private static readonly TypeTree _waypoint = TypeTree.Build("Waypoint", [Node(0, "Waypoint", "Base"), Node(1, "int", "id", 4), Node(1, "int", "mapId", 4)]);

    [Fact]
    public void EachKeyNamesItsRecordInTheRegistry()
    {
        var rows = DataTable.Read(Table(keys: [2, 1], values: [11, 10], records: [(10, 1, 100), (11, 2, 200)], nulls: 1));

        Assert.Equal([2L, 1L], rows.Select(row => row.Key));
        Assert.Equal([200, 100], rows.Select(row => row.Record.Int32("mapId")));
        Assert.Equal([2L, 1L], DataTable.ById("waypoints", rows).Select(record => record.Integer("id")));
    }

    [Fact]
    public void AKeyWithoutRecordIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => DataTable.Read(Table(keys: [1], values: [99], records: [(10, 1, 100)])));
    }

    [Fact]
    public void AsManyKeysAsRecordsAreNeeded()
    {
        Assert.Throws<InvalidDataException>(() => DataTable.Read(Table(keys: [1, 2], values: [10], records: [(10, 1, 100)])));
    }

    [Fact]
    public void ARecordKeyedUnderAnotherIdIsRefused()
    {
        var rows = DataTable.Read(Table(keys: [5], values: [10], records: [(10, 1, 100)]));

        Assert.Throws<InvalidDataException>(() => DataTable.ById("waypoints", rows));
    }

    [Fact]
    public void ARegistryOfAnotherVersionIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => DataTable.Read(Table(keys: [], values: [], records: [], version: 3)));
    }

    [Fact]
    public void ARecordOfAClassTheFileDoesNotDescribeIsRefused()
    {
        var table = Table(keys: [1], values: [10], records: [(10, 1, 100)]);

        Assert.Throws<InvalidDataException>(() => DataTable.Read(new SerializedObject { PathId = 1, Name = "", Tree = table.Tree, Data = table.Data, ReferenceTypes = new Dictionary<string, TypeTree>() }));
    }

    [Fact]
    public void TheInstalledClientsZaapsAreKeyedByTheirIds()
    {
        var client = Installed.Client();
        var zaaps = DataTable.ReadById(client, "waypoints");

        Assert.NotEmpty(zaaps);
        Assert.All(zaaps, zaap => Assert.True(zaap.Integer("mapId") > 0));
    }

    /// <summary>A DataRoot as the client lays it out: keys, references to records, then the registry holding them.</summary>
    private static SerializedObject Table(long[] keys, long[] values, (long Rid, int Id, int MapId)[] records, int nulls = 0, int version = 2)
    {
        FlatNode[] nodes =
        [
            Node(0, "WaypointsDataRoot", "Base"),
            Node(1, "SerializableDictionary", "objectsById"),
            .. Vector(2, "m_keys", "SInt64", 8),
            Node(2, "vector", "m_values"), Array(3), Node(4, "int", "size", 4), Node(4, "ReferencedObject", "data"), Node(5, "SInt64", "rid", 8),
            new FlatNode { Level = 1, Type = "ManagedReferencesRegistry", Name = "references", ByteSize = -1, IsRegistry = true },
            Node(2, "int", "version", 4),
            Node(2, "vector", "RefIds"), Array(3), Node(4, "int", "size", 4), Node(4, "ReferencedObject", "data"),
            Node(5, "SInt64", "rid", 8),
            Node(5, "ReferencedManagedType", "type"), .. String(6, "class"), .. String(6, "ns"), .. String(6, "asm"),
            Node(5, "ReferencedObjectData", "data"),
        ];
        var data = new List<byte>();

        data.AddRange(Bytes(keys.Length));
        data.AddRange(keys.SelectMany(BitConverter.GetBytes));
        data.AddRange(Bytes(values.Length));
        data.AddRange(values.SelectMany(BitConverter.GetBytes));
        data.AddRange(Bytes(version, records.Length + nulls));

        foreach (var (rid, id, mapId) in records)
        {
            data.AddRange(BitConverter.GetBytes(rid));
            data.AddRange([.. Bytes(8), .. "Waypoint"u8, .. Bytes(4), .. "Core"u8, .. Bytes(4), .. "Data"u8]);
            data.AddRange(Bytes(id, mapId));
        }

        for (var i = 0; i < nulls; i++)
        {
            data.AddRange(BitConverter.GetBytes(-2L));
            data.AddRange(Bytes(0, 0, 0));
        }

        return new SerializedObject
        {
            PathId = 1,
            Name = "",
            Tree = TypeTree.Build("WaypointsDataRoot", nodes),
            Data = [.. data],
            ReferenceTypes = new Dictionary<string, TypeTree> { ["Core.Waypoint"] = _waypoint },
        };
    }
}
