using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.Unity;

public sealed class FieldsTests
{
    [Fact]
    public void ARecordsFieldsAreReadByTheirNames()
    {
        var tree = Of(
            One(Node(1, "int", "<id>k__BackingField", 4)),
            One(Node(1, "UInt8", "activated", 1, align: true)),
            String(1, "name"),
            Vector(1, "monsters", "int", 4),
            [Node(1, "Point", "origin"), Node(2, "SInt16", "x", 2), Node(2, "SInt16", "y", 2)],
            [Node(1, "vector", "zooms"), Array(2), Node(3, "int", "size", 4), Node(3, "Zoom", "data"), Node(4, "float", "scale", 4)],
            One(Node(1, "UInt16", "subAreaId", 2)));
        byte[] data =
        [
            .. Bytes(17), 1, 0, 0, 0, .. Bytes(2), (byte)'O', (byte)'k', 0, 0, .. Bytes(2, 31, 47), 0xFE, 0xFF, 3, 0,
            .. Bytes(1), .. BitConverter.GetBytes(0.5f), 9, 0,
        ];
        var reader = new ObjectReader(data, tree);

        var record = Fields.Read(ref reader, 0, "Waypoint");

        reader.ExpectEnd();
        Assert.Equal(17, record.Int32("id"));
        Assert.True(record.Boolean("activated"));
        Assert.Equal("Ok", record.String("name"));
        Assert.Equal([31, 47], record.Int32Array("monsters"));
        Assert.Equal(-2, record.Nested("origin").Int16("x"));
        Assert.Equal(0.5f, Assert.Single(record.Items("zooms")).Single("scale"));
        Assert.Equal(9, record.UInt16("subAreaId"));
        Assert.Equal(9L, record.Integer("subAreaId"));
    }

    [Fact]
    public void AMissingFieldIsNamedInTheError()
    {
        var record = Read(One(Node(1, "int", "id", 4)), Bytes(1));

        var error = Assert.Throws<InvalidDataException>(() => record.Int32("mapId"));

        Assert.Contains("Test has no field mapId", error.Message, StringComparison.Ordinal);
        Assert.False(record.Has("mapId"));
    }

    [Fact]
    public void AFieldOfAnotherWidthIsRefusedByItsExactGetter()
    {
        var record = Read(One(Node(1, "SInt16", "posX", 2)), [5, 0]);

        Assert.Throws<InvalidDataException>(() => record.Int32("posX"));
        Assert.Equal(5L, record.Integer("posX"));
    }

    [Fact]
    public void AByteIsAFlagOnlyWhenItIsZeroOrOne()
    {
        var record = Read(One(Node(1, "UInt8", "flag", 1)), [2]);

        Assert.Throws<InvalidDataException>(() => record.Boolean("flag"));
        Assert.Equal(2, record.Byte("flag"));
    }

    [Fact]
    public void FieldsNoTableReadAreSkippedWhole()
    {
        var record = Read([.. Vector(1, "bytes", "UInt8", 1), Node(1, "int", "id", 4)], [.. Bytes(3), 1, 2, 3, 0, .. Bytes(8)]);

        Assert.False(record.Has("bytes"));
        Assert.Equal(8, record.Int32("id"));
    }

    private static Fields Read(IEnumerable<FlatNode> fields, byte[] data)
    {
        var reader = new ObjectReader(data, Of(fields));
        var record = Fields.Read(ref reader, 0, "Test");

        reader.ExpectEnd();

        return record;
    }
}
