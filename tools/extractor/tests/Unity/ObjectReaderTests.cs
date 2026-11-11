using System.Text;
using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.Unity;

public sealed class ObjectReaderTests
{
    [Fact]
    public void AnAlignedFieldMovesTheNextOneToAFourByteBoundary()
    {
        var tree = Of(One(Node(1, "UInt8", "flag", 1, align: true)), One(Node(1, "int", "id", 4)));
        var reader = new ObjectReader([1, 0, 0, 0, 7, 0, 0, 0], tree);

        Assert.True(reader.ReadBoolean(1));
        Assert.Equal(7, reader.ReadInt32(2));
        reader.ExpectEnd();
    }

    [Fact]
    public void AStringIsItsLengthThenItsBytesAligned()
    {
        var tree = Of(String(1, "name"), One(Node(1, "int", "id", 4)));
        byte[] data = [.. BitConverter.GetBytes(5), .. Encoding.UTF8.GetBytes("Astrub"[..5]), 0, 0, 0, .. BitConverter.GetBytes(9)];
        var reader = new ObjectReader(data, tree);

        Assert.Equal("Astru", reader.ReadString(1));
        Assert.Equal(9, reader.ReadInt32(5));
        reader.ExpectEnd();
    }

    [Theory]
    [InlineData("int", 4, -2L)]
    [InlineData("SInt32", 4, -2L)]
    [InlineData("unsigned int", 4, 4294967294L)]
    [InlineData("UInt32", 4, 4294967294L)]
    [InlineData("SInt64", 8, -2L)]
    [InlineData("long long", 8, -2L)]
    [InlineData("UInt64", 8, -2L)]
    [InlineData("unsigned long long", 8, -2L)]
    [InlineData("SInt16", 2, -2L)]
    [InlineData("short", 2, -2L)]
    [InlineData("UInt16", 2, 65534L)]
    [InlineData("unsigned short", 2, 65534L)]
    [InlineData("SInt8", 1, -2L)]
    [InlineData("char", 1, -2L)]
    [InlineData("UInt8", 1, 254L)]
    [InlineData("unsigned char", 1, 254L)]
    public void EachIntegerIsReadByEitherOfItsTypeNames(string type, int size, long expected)
    {
        var tree = Of(One(Node(1, type, "value", size)));
        var reader = new ObjectReader([.. Enumerable.Repeat((byte)0xFF, size - 1).Prepend((byte)0xFE)], tree);

        long value = (type, size) switch
        {
            ("UInt64" or "unsigned long long", _) => (long)reader.ReadUInt64(1),
            (_, 8) => reader.ReadInt64(1),
            ("unsigned int" or "UInt32", _) => reader.ReadUInt32(1),
            (_, 4) => reader.ReadInt32(1),
            ("UInt16" or "unsigned short", _) => reader.ReadUInt16(1),
            (_, 2) => reader.ReadInt16(1),
            ("UInt8" or "unsigned char", _) => reader.ReadByte(1),
            _ => reader.ReadSByte(1),
        };

        Assert.Equal(expected, value);
        reader.ExpectEnd();
    }

    [Theory]
    [InlineData("bool")]
    [InlineData("UInt8")]
    public void AFlagIsABoolOrAByte(string type)
    {
        var tree = Of(One(Node(1, type, "flag", 1)));
        var reader = new ObjectReader([1], tree);

        Assert.True(reader.ReadBoolean(1));
    }

    [Fact]
    public void AFloatIsReadAsOne()
    {
        var tree = Of(One(Node(1, "float", "x", 4)));
        var reader = new ObjectReader(BitConverter.GetBytes(1.5f), tree);

        Assert.Equal(1.5f, reader.ReadSingle(1));
    }

    [Fact]
    public void AnythingButAStringIsNotReadAsOne()
    {
        var tree = Of(One(Node(1, "int", "id", 4)));

        Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader(Bytes(0), tree);

            reader.ReadString(1);
        });
    }

    [Fact]
    public void AFieldOfAnotherTypeIsRefused()
    {
        var tree = Of(One(Node(1, "float", "x", 4)));

        var error = Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader(Bytes(0), tree);

            reader.ReadInt32(1);
        });

        Assert.Contains("Test.x is a float", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SkippedFieldsAreSteppedOverWholeAndAligned()
    {
        var tree = Of(
            Vector(1, "bytes", "UInt8", 1),
            String(1, "name"),
            [Node(1, "vector", "points"), Array(2), Node(3, "int", "size", 4), Node(3, "Point", "data"), Node(4, "SInt16", "x", 2), Node(4, "SInt16", "y", 2)],
            One(Node(1, "int", "last", 4)));
        byte[] data = [.. Bytes(3), 1, 2, 3, 0, .. Bytes(1), (byte)'a', 0, 0, 0, .. Bytes(2), 1, 0, 2, 0, 3, 0, 4, 0, .. Bytes(42)];
        var reader = new ObjectReader(data, tree);

        reader.Skip(1);
        reader.Skip(5);
        reader.Skip(9);

        Assert.Equal(42, reader.ReadInt32(tree.Child(0, "last")));
        reader.ExpectEnd();
    }

    [Fact]
    public void ARegistryInsideATreeTakesNoBytes()
    {
        var tree = Of(
            [new FlatNode { Level = 1, Type = "ManagedReferencesRegistry", Name = "references", ByteSize = -1, IsRegistry = true }, Node(2, "int", "version", 4)],
            One(Node(1, "int", "id", 4)));
        var reader = new ObjectReader(Bytes(7), tree);

        reader.Skip(1);

        Assert.Equal(7, reader.ReadInt32(3));
        reader.ExpectEnd();
    }

    [Fact]
    public void AStructArrayWithANegativeCountIsRefused()
    {
        var tree = Of([Node(1, "vector", "points"), Array(2), Node(3, "int", "size", 4), Node(3, "Point", "data"), Node(4, "SInt16", "x", 2)]);

        Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader(Bytes(-1), tree);

            reader.Skip(1);
        });
    }

    [Fact]
    public void ALeafOfNoFixedSizeCannotBeSkipped()
    {
        var tree = Of(One(Node(1, "Unknown", "blob")));

        var error = Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader(Bytes(0), tree);

            reader.Skip(1);
        });

        Assert.Contains("no fixed size", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnArrayIsBoundedByTheSizeOfItsItems()
    {
        var tree = Of(Vector(1, "cells", "int", 4));

        var error = Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader(Bytes(2, 1), tree);

            reader.Skip(1);
        });

        Assert.Contains("claims 2 items", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnArrayClaimingMoreItemsThanBytesLeftIsRefusedBeforeAnyAllocation()
    {
        var tree = Of(Vector(1, "cells", "int", 4));

        Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader(Bytes(1_000_000_000, 1), tree);

            reader.Skip(1);
        });
    }

    [Fact]
    public void AnObjectThatEndsEarlyFails()
    {
        var tree = Of(One(Node(1, "SInt64", "id", 8)));

        var error = Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader([1, 2, 3], tree);

            reader.ReadInt64(1);
        });

        Assert.Contains("ends early", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BytesLeftAfterTheObjectMeanItsLayoutWasMisread()
    {
        var tree = Of(One(Node(1, "int", "id", 4)));

        Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader(Bytes(1, 2), tree);

            reader.ReadInt32(1);
            reader.ExpectEnd();
        });
    }
}
