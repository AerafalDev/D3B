using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace D3B.Bot.Reports;

/// <summary>Reads a report as untrusted input: a zip anyone could have posted, bounded in its entries and their expanded size, whose files are checked against its manifest and never run (D410).</summary>
public static partial class ReportReader
{
    /// <summary>Reads and checks a report.</summary>
    /// <param name="input">The zip.</param>
    /// <param name="limits">The bounds, <see cref="ReportLimits.Default"/> when null.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ReportFormatException">It is no report, goes past a bound, or holds a file its manifest does not list or lists otherwise.</exception>
    public static ReportBundle Read(Stream input, ReportLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        limits ??= ReportLimits.Default;

        try
        {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, true);

            if (zip.Entries.Count > limits.MaxEntries)
                throw new ReportFormatException($"The report holds {zip.Entries.Count} entries, more than the {limits.MaxEntries} a report may.");

            var total = 0L;
            var read = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in zip.Entries)
            {
                if (!ReportFiles.IsValid(entry.FullName))
                    throw new ReportFormatException($"The report holds '{Printable(entry.FullName)}', which is no file of a report.");

                if (read.Keys.Any(name => string.Equals(name, entry.FullName, StringComparison.OrdinalIgnoreCase)))
                    throw new ReportFormatException($"The report holds '{entry.FullName}' twice.");

                var most = entry.FullName == ReportFiles.Manifest ? limits.MaxManifestBytes : limits.MaxEntryBytes;
                var bytes = Expand(entry, Math.Min(most, limits.MaxTotalBytes - total));

                total += bytes.Length;
                read[entry.FullName] = bytes;
            }

            return Check(read);
        }
        catch (InvalidDataException exception)
        {
            throw new ReportFormatException("The file is no zip, or is a damaged one.", exception);
        }
    }

    // The size a zip declares is its sender's word: the bytes are counted as they come, and the entry refused past the bound.
    private static byte[] Expand(ZipArchiveEntry entry, long most)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int count;

        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memory.Length + count > most)
                throw new ReportFormatException($"'{entry.FullName}' expands past the {most:N0} bytes a report may hold.");

            memory.Write(buffer, 0, count);
        }

        return memory.ToArray();
    }

    private static ReportBundle Check(Dictionary<string, byte[]> read)
    {
        if (!read.Remove(ReportFiles.Manifest, out var manifestBytes))
            throw new ReportFormatException("The report has no manifest.");

        ReportManifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize(manifestBytes, ReportJson.Default.ReportManifest);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new ReportFormatException("The manifest does not read.", exception);
        }

        if (manifest is null || !ReportCode.IsValid(manifest.Code) || manifest.Files is null || manifest.Files.Any(file => file is null || !ReportFiles.IsValid(file.Name) || file.Sha256 is null || file.Size < 0))
            throw new ReportFormatException("The manifest has no valid code, or a list of files with a file that is none.");

        // It is printed by a tool and typed in a command: a version is numbers and dots, the rest is short text with nothing to break a line.
        if (manifest.Client is null || !VersionPattern().IsMatch(manifest.Client) || !IsShort(manifest.App) || (manifest.Pack is not null && !IsShort(manifest.Pack)) || (manifest.Mapping is not null && !IsShort(manifest.Mapping)))
            throw new ReportFormatException("The manifest names versions that are none.");

        var listed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in manifest.Files)
        {
            if (!listed.Add(file.Name) || !read.TryGetValue(file.Name, out var bytes))
                throw new ReportFormatException($"The manifest lists '{Printable(file.Name)}', which the report does not hold, or twice.");

            if (bytes.Length != file.Size || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), file.Sha256, StringComparison.Ordinal))
                throw new ReportFormatException($"'{file.Name}' is not the file the manifest lists: its size or its SHA-256 differs.");
        }

        if (read.Keys.FirstOrDefault(name => !listed.Contains(name)) is { } unlisted)
            throw new ReportFormatException($"The report holds '{unlisted}', which the manifest does not list.");

        if (manifest.HasMessage != read.ContainsKey(ReportFiles.Message))
            throw new ReportFormatException("The manifest and the report disagree on whether there is a message.");

        if (read.TryGetValue(ReportFiles.Message, out var message) && (message.Length > ReportContent.MessageLimit * 3 || System.Text.Encoding.UTF8.GetString(message).Length > ReportContent.MessageLimit))
            throw new ReportFormatException($"The message holds more than the {ReportContent.MessageLimit} characters a message may.");

        return new ReportBundle(manifest, read);
    }

    private static bool IsShort(string? text)
    {
        return text is { Length: > 0 and <= 64 } && text.All(character => !char.IsControl(character));
    }

    [GeneratedRegex(@"^[0-9]+(\.[0-9]+){0,5}\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    // A name from a stranger is printed with its control characters replaced.
    private static string Printable(string? text)
    {
        return new string([.. (text ?? string.Empty).Take(100).Select(c => char.IsControl(c) ? '?' : c)]);
    }
}
