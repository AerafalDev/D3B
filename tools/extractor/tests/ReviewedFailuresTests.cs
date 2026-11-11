using System.Text;
using D3B.Extractor.Code;
using D3B.Extractor.Graph;
using D3B.Extractor.Maps;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;
using D3B.Extractor.World;
using D3B.Extractor.WorldMaps;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests;

/// <summary>The failure cases the extractor's review asked for, each a client the extractor must refuse with an error.</summary>
public sealed class ReviewedFailuresTests
{
    [Fact]
    public void AClientWithoutOneOfTheFiveLanguagesIsRefused()
    {
        using var temporary = new TemporaryFolder();
        var root = temporary.Client("Version=3.7.13.17");
        var texts = Path.Combine(root, "Dofus_Data", "StreamingAssets", "Content", "I18n");

        Directory.CreateDirectory(texts);
        File.WriteAllBytes(Path.Combine(texts, "fr.bin"), EmptyTexts());

        var error = Assert.Throws<InvalidDataException>(() => TextFiles.Open(ClientInstall.Locate(root)));

        Assert.Contains("no en texts", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextFilesAreOnePerLanguage()
    {
        Assert.Throws<ArgumentException>(() => TextFiles.From([EmptyTexts()]));
    }

    [Fact]
    public void AFileThatHoldsTextZeroIsRefused()
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.UTF8);

        writer.Write("fr");
        writer.Write(1);
        writer.Write(0);
        writer.Write(3 + 4 + 8 + 4);
        writer.Write(0);
        writer.Write("none");
        writer.Flush();

        var files = TextFiles.Languages.Select(_ => buffer.ToArray());

        Assert.Throws<InvalidDataException>(() => TextFiles.From(files));
    }

    [Fact]
    public void AFieldAndABackingFieldOfOneNameFailOnlyWhenAsked()
    {
        var tree = Of(One(Node(1, "int", "id", 4)), One(Node(1, "int", "<id>k__BackingField", 4)), One(Node(1, "int", "mapId", 4)));
        var reader = new ObjectReader(Bytes(1, 2, 3), tree);
        var record = Fields.Read(ref reader, 0, "Test");

        Assert.Equal(3, record.Int32("mapId"));
        Assert.Throws<InvalidDataException>(() => record.Int32("id"));
    }

    [Fact]
    public void ARegistryWithoutVersionIsRefused()
    {
        var tree = Of([new FlatNode { Level = 1, Type = "ManagedReferencesRegistry", Name = "references", ByteSize = -1, IsRegistry = true }, Node(2, "vector", "RefIds"), Array(3), Node(4, "int", "size", 4), Node(4, "ReferencedObject", "data"), Node(5, "SInt64", "rid", 8), Node(5, "ReferencedManagedType", "type"), .. String(6, "class"), .. String(6, "ns"), .. String(6, "asm")]);

        Assert.Throws<InvalidDataException>(() =>
        {
            var reader = new ObjectReader(Bytes(0), tree);

            ReferenceRegistry.Read(ref reader, Bytes(0), 1, new Dictionary<string, TypeTree>(), (long _, string _, ref ObjectReader _) => true);
        });
    }

    [Fact]
    public void AnObjectTheVisitorLeavesIsSkippedWholeAndTheItemAligned()
    {
        var registry = Of([
            new FlatNode { Level = 1, Type = "ManagedReferencesRegistry", Name = "references", ByteSize = -1, IsRegistry = true },
            Node(2, "int", "version", 4),
            Node(2, "vector", "RefIds"), Array(3), Node(4, "int", "size", 4), Node(4, "ReferencedObject", "data", align: true),
            Node(5, "SInt64", "rid", 8), Node(5, "ReferencedManagedType", "type"), .. String(6, "class"), .. String(6, "ns"), .. String(6, "asm"),
        ]);
        var flag = TypeTree.Build("Flag", [Node(0, "Flag", "Base"), Node(1, "UInt8", "on", 1)]);
        byte[] data =
        [
            .. Bytes(2, 2),
            .. BitConverter.GetBytes(10L), .. Bytes(4), .. "Flag"u8, .. Bytes(0, 0), 1, 0, 0, 0,
            .. BitConverter.GetBytes(11L), .. Bytes(4), .. "Flag"u8, .. Bytes(0, 0), 0, 0, 0, 0,
        ];
        var reader = new ObjectReader(data, registry);
        var seen = new List<long>();

        ReferenceRegistry.Read(ref reader, data, 1, new Dictionary<string, TypeTree> { ["Flag"] = flag }, (long id, string _, ref ObjectReader _) =>
        {
            seen.Add(id);

            return false;
        });

        reader.ExpectEnd();
        Assert.Equal([10L, 11L], seen);
    }

    [Fact]
    public void AClientWithoutItsCodeOrWithOtherMetadataIsRefused()
    {
        using var temporary = new TemporaryFolder();
        var root = temporary.Client("Version=3.7.13.17");
        var client = ClientInstall.Locate(root);
        var metadata = Path.Combine(root, "Dofus_Data", "il2cpp_data", "Metadata");

        Assert.Throws<InvalidDataException>(() => ClientCode.Open(client));

        Directory.CreateDirectory(metadata);
        File.WriteAllBytes(Path.Combine(root, "GameAssembly.dll"), []);
        File.WriteAllBytes(Path.Combine(root, "UnityPlayer.dll"), []);
        File.WriteAllBytes(Path.Combine(metadata, "global-metadata.dat"), [.. BitConverter.GetBytes(0xFAB11BAFu), .. BitConverter.GetBytes(40)]);

        var error = Assert.Throws<InvalidDataException>(() => ClientCode.Open(client));

        Assert.Contains("version 40", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIntBackedFlagOfBit31IsNamed()
    {
        var flags = FlagEnum.From("Test", [("High", 1UL << 31)]);

        Assert.Equal(["high"], flags.Names(1UL << 31));
    }

    [Fact]
    public void AZaapOnNoMapIsRefused()
    {
        var tree = Of(One(Node(1, "int", "id", 4)), One(Node(1, "int", "mapId", 4)), One(Node(1, "int", "subAreaId", 4)), One(Node(1, "UInt8", "activated", 1)));
        var reader = new ObjectReader([.. Bytes(1, 0, 95), 1], tree);
        var zaap = Fields.Read(ref reader, 0, "Waypoint");

        Assert.Throws<InvalidDataException>(() => WorldTables.ReadZaaps([zaap]));
    }

    [Fact]
    public void AFailedExtractionLeavesNoStagingFolder()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "data");

        using (var folder = DataFolder.Create(target))
        {
            folder.Write("areas.json", "[]"u8);
        }

        Assert.Empty(Directory.GetDirectories(temporary.Path));
    }

    [Fact]
    public void APositionOnAMapTheClientLacksIsRefused()
    {
        var graph = new WorldGraph { Vertices = [], Edges = [] };
        MapPosition[] positions = [new() { Id = 9, X = 0, Y = 0, SubAreaId = 1, WorldMap = 1, Name = null, Flags = [] }];

        Assert.Throws<InvalidDataException>(() => GraphTables.Check(graph, new Dictionary<long, MapData>(), positions, []));
    }

    [Fact]
    public void ACatalogKeyOfTwoAddressesIsRefused()
    {
        var data = new byte[300];

        Put(data, 0, "worldmaps/24");
        Put(data, 26, "75/1.jpg");
        Put(data, 46, "75/2.jpg");
        BitConverter.GetBytes(4).CopyTo(data, 70);
        BitConverter.GetBytes(30).CopyTo(data, 100);
        BitConverter.GetBytes(70).CopyTo(data, 104);
        Put(data, 108, "0123456789abcdef0123456789abcdef");
        BitConverter.GetBytes(50).CopyTo(data, 150);
        BitConverter.GetBytes(70).CopyTo(data, 154);
        Put(data, 158, "0123456789abcdef0123456789abcdef");

        Assert.Throws<InvalidDataException>(() => AddressablesCatalog.AddressesByKey(data));
    }

    private static byte[] EmptyTexts()
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.UTF8);

        writer.Write("fr");
        writer.Write(0);
        writer.Write(0);
        writer.Flush();

        return buffer.ToArray();
    }

    private static void Put(byte[] data, int lengthAt, string text)
    {
        BitConverter.GetBytes(text.Length).CopyTo(data, lengthAt);
        Encoding.ASCII.GetBytes(text).CopyTo(data, lengthAt + 4);
    }
}
