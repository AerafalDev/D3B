using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;

namespace D3B.Extractor;

/// <summary>The data folder's release asset, <c>client-&lt;build&gt;-format&lt;format&gt;.tar.gz</c>, which every <c>tar</c> reads (ADR 0008).</summary>
/// <remarks>
/// Its entries are the folder's files at its root, in order of their paths, with a fixed date and no owner, so that two
/// extractions of one build by one extractor give the same bytes.
/// </remarks>
internal static class DataArchive
{
    private static readonly DateTimeOffset _date = DateTimeOffset.UnixEpoch;

    /// <summary>The asset's file name for a build.</summary>
    /// <remarks>
    /// It names the format, since a published asset is never replaced: a build's data in a new format is a second asset
    /// of the build's release (the owner, 2026-11-13). Format 1's asset was <c>client-&lt;build&gt;.tar.gz</c>.
    /// </remarks>
    public static string NameOf(string build)
    {
        return string.Create(CultureInfo.InvariantCulture, $"client-{build}-format{DataFolder.Format}.tar.gz");
    }

    /// <summary>Writes the archive of the data folder <paramref name="folder"/> to <paramref name="path"/>, replacing it.</summary>
    public static void Write(string folder, string path)
    {
        var staging = path + ".d3b-staging";
        var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(file => (Full: file, Name: Path.GetRelativePath(folder, file).Replace('\\', '/')))
            .OrderBy(file => file.Name, StringComparer.Ordinal);

        using (var stream = File.Create(staging))
        using (var gzip = new GZipStream(stream, CompressionLevel.Optimal))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Ustar, false))
        {
            foreach (var (full, name) in files)
            {
                using var content = File.OpenRead(full);

                tar.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = content,
                    ModificationTime = _date,
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
                });
            }
        }

        File.Move(staging, path, true);
    }
}
