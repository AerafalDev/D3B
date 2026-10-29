using System.Security.Cryptography;
using System.Text;
using D3B.Data.Banks;
using D3B.Data.Icons;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Texts;
using D3B.Data.World;
using MemoryPack;

namespace D3B.Data.Tests.Packs;

/// <summary>
/// Fails when what a pack is written as changes while <see cref="GamePack.FormatVersion"/> stays: a host would then
/// misread the packs of its own format (D25).
/// </summary>
/// <remarks>
/// The layout is the SHA-256 of what defines the format: the header and trailer, and, for every type a pack holds, its
/// full name, which the index records and a read checks, and the bytes MemoryPack writes for a sample of it. Every field
/// of a sample has its own value, so that moving, retyping or adding a field changes the bytes; renaming a field does
/// not, and is no format change. The bytes are taken before compression, which changes with zstd's version, not with
/// the format.
/// </remarks>
public sealed class PackFormatTests
{
    // The layout of each format since this record began, oldest first. Once a pack of a format is published, its line is
    // never edited: a changed layout is a new format, and a new line. Format 1, session 2a, held only texts and predates
    // the record. The samples below are part of the layouts recorded here, so they change only with a new format.
    private static readonly (int Format, string Layout)[] _formats =
    [
        (2, "44e092e069deeecb28f1afb3f0619ff7921a30003a89974ecd5d5a93923533c8"),
        (3, "94032ad3554bc9df362222810fcb95534e53d56997b1dc1a839ac598dde39ede"),
        (4, "4f0e5099257dbba4ee548612249d8a5c9c9da7387fdfc765b15132d9c81fa222"),
    ];

    private static readonly string[] _criteria = ["c24"];
    private static readonly int[] _harvestables = [43];
    private static readonly int[] _replies = [67];
    private static readonly int[] _itemIcons = [71];
    private static readonly int[] _jobIcons = [72];

    [Fact]
    public void TheLastRecordedFormatIsTheCurrentOne()
    {
        var format = _formats[^1].Format;

        Assert.True(
            format == GamePack.FormatVersion,
            $"GamePack.FormatVersion is {GamePack.FormatVersion}, where the last recorded format is {format}: record the layout of format {GamePack.FormatVersion}.");

        for (var i = 1; i < _formats.Length; i++)
            Assert.True(_formats[i].Format > _formats[i - 1].Format, "The recorded formats must be in ascending order.");
    }

    [Fact]
    public void TheSerializedLayoutChangesOnlyWithTheFormatVersion()
    {
        var (format, recorded) = _formats[^1];
        var current = Layout();

        Assert.True(
            current == recorded,
            $"The pack's layout is now {current}, where format {format} recorded {recorded}: raise GamePack.FormatVersion, then record the new layout under it.");
    }

    private static string Layout()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        hash.AppendData(PackLayout.Magic);
        hash.AppendData(BitConverter.GetBytes(PackLayout.HeaderLength));
        hash.AppendData(BitConverter.GetBytes(PackLayout.TrailerLength));

        Append(hash, new PackIndex
        {
            Info = new PackInfo
            {
                ClientVersion = "3.7.12.12",
                ClientBuildDate = new DateTimeOffset(2026, 10, 19, 15, 11, 45, TimeSpan.FromHours(2)),
                Fingerprint = "f1",
                ExtractorVersion = "e2",
            },
            Entries = [new PackEntry { Name = "n3", Type = "t4", Offset = 5, CompressedLength = 6, Length = 7 }],
        });
        Append(hash, new TextTable("fr", [8, 9], ["a", "b"]));
        Append(hash, new MapData(
            10,
            11,
            12,
            13,
            14,
            Enumerable.Repeat(new MapCell(MapCellProperties.Walkable | MapCellProperties.Blue, -130, 15, 0x83, 1, 0x21, 9), MapData.CellCount).ToArray(),
            new MapInteractive[] { new(16, 17, 70) }));
        Append(hash, new WorldGraph(
            new WorldVertex[] { new(18, 2), new(19, 3) },
            new WorldEdge[] { new(0, 1, 0, 1) },
            new WorldTransition[] { new(WorldTransitionType.Interactive, 5, 20, 21, 22, 23, 0) },
            _criteria));
        Append(hash, new WorldAtlas(
            new MapPosition[] { new(25, 26, 27, 28, 29, 30, 31) },
            new SubArea[] { new(32, 33, 34, 35, 36, true, 0, 1) },
            new Area[] { new(37, 38, 39) },
            new Waypoint[] { new(40, 41, 42, true) },
            _harvestables));
        Append(hash, new ItemCatalog(new Item[] { new(44, 45, 46, 47, 48, 68) }, new ItemType[] { new(49, 50, 51, 52) }));
        Append(hash, new JobCatalog(new Job[] { new(53, 54, 69) }, new Skill[] { new(55, 56, 57, 58, 59, 60, true) }));
        Append(hash, new InteractiveCatalog(new Interactive[] { new(61, 62) }));
        Append(hash, new BankCatalog(new Bank[] { new(63, 64) }, new Banker[] { new(65, 66, 0, 1) }, _replies));
        Append(hash, new IconCatalog(new IconTable(_itemIcons, Pixels(73)), new IconTable(_jobIcons, Pixels(74))));

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static byte[] Pixels(int seed)
    {
        return [.. Enumerable.Range(seed, IconTable.IconLength).Select(value => (byte)value)];
    }

    private static void Append<T>(IncrementalHash hash, T sample)
    {
        var bytes = MemoryPackSerializer.Serialize(sample);

        hash.AppendData(Encoding.UTF8.GetBytes(typeof(T).FullName!));
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }
}
