using System.Formats.Tar;
using System.IO.Compression;

namespace D3B.Extractor.Tests;

public sealed class DataArchiveTests
{
    [Fact]
    public void TheArchiveHoldsTheFolderAtItsRootInOrderAndTheSameBytesTwice()
    {
        using var temporary = new TemporaryFolder();
        var target = Path.Combine(temporary.Path, "3.7.13.17");
        var folder = DataFolder.Create(target);

        folder.Write("maps/7.json", "[]\n"u8);
        folder.Write("areas.json", "[]\n"u8);
        folder.Complete("3.7.13.17", "0.1.0");

        var first = Path.Combine(temporary.Path, DataArchive.NameOf("3.7.13.17"));
        var second = Path.Combine(temporary.Path, "again.tar.gz");

        DataArchive.Write(target, first);
        DataArchive.Write(target, second);

        Assert.Equal("client-3.7.13.17-format3.tar.gz", Path.GetFileName(first));
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        Assert.Equal(["areas.json", "manifest.json", "maps/7.json"], Entries(first).Select(entry => entry.Name));
        Assert.All(Entries(first), entry => Assert.Equal(DateTimeOffset.UnixEpoch, entry.ModificationTime));
    }

    private static List<(string Name, DateTimeOffset ModificationTime)> Entries(string path)
    {
        using var stream = File.OpenRead(path);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        var entries = new List<(string, DateTimeOffset)>();

        while (tar.GetNextEntry() is { } entry)
        {
            entries.Add((entry.Name, entry.ModificationTime));
        }

        return entries;
    }
}
