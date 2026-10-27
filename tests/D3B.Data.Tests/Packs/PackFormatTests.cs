using System.Security.Cryptography;
using System.Text;
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
    ];

    private static readonly string[] _criteria = ["c24"];

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
            new MapInteractive[] { new(16, 17) }));
        Append(hash, new WorldGraph(
            new WorldVertex[] { new(18, 2), new(19, 3) },
            new WorldEdge[] { new(0, 1, 0, 1) },
            new WorldTransition[] { new(WorldTransitionType.Interactive, 5, 20, 21, 22, 23, 0) },
            _criteria));
        Append(hash, new WorldAtlas(
            new MapPosition[] { new(25, 26, 27, 28, 29, 30, 31) },
            new SubArea[] { new(32, 33, 34, 35, 36, true) },
            new Area[] { new(37, 38, 39) },
            new Waypoint[] { new(40, 41, 42, true) }));

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append<T>(IncrementalHash hash, T sample)
    {
        var bytes = MemoryPackSerializer.Serialize(sample);

        hash.AppendData(Encoding.UTF8.GetBytes(typeof(T).FullName!));
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }
}
