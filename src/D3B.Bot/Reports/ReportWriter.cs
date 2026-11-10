using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace D3B.Bot.Reports;

/// <summary>Writes a report: one zip, the manifest last, listing every other file with its size and SHA-256 (D410, D411).</summary>
public static class ReportWriter
{
    /// <summary>Writes a report.</summary>
    /// <param name="content">What it is made of.</param>
    /// <param name="output">Where to write the zip.</param>
    /// <returns>The manifest it carries.</returns>
    /// <exception cref="ArgumentException">The code is none, a report of the game has no capture, the message is too long, or a part's name is none of a report's.</exception>
    public static ReportManifest Write(ReportContent content, Stream output)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(output);

        if (!ReportCode.IsValid(content.Code))
            throw new ArgumentException($"'{content.Code}' is no report code.", nameof(content));

        if (content.Area.NeedsCapture() && content.Capture is null)
            throw new ArgumentException("A report of the game cannot go without its capture: the player cancels instead (D410).", nameof(content));

        if (content.Message is { Length: > ReportContent.MessageLimit })
            throw new ArgumentException($"A message holds at most {ReportContent.MessageLimit} characters (D411).", nameof(content));

        var files = new List<(string Name, byte[] Data)>();
        var hasMessage = !string.IsNullOrEmpty(content.Message);

        if (hasMessage)
            files.Add((ReportFiles.Message, Encoding.UTF8.GetBytes(content.Message!)));

        if (content.Capture is { } capture)
            files.Add((ReportFiles.Capture, capture.Bytes));

        foreach (var part in content.Parts)
        {
            if (!ReportFiles.IsValid(part.Name) || part.Name is ReportFiles.Manifest or ReportFiles.Message or ReportFiles.Capture || files.Any(file => file.Name == part.Name))
                throw new ArgumentException($"'{part.Name}' is no file a report can hold, or is there twice.", nameof(content));

            files.Add((part.Name, part.Data));
        }

        var manifest = new ReportManifest
        {
            Code = content.Code,
            CreatedAt = content.CreatedAt,
            App = content.App,
            Client = content.Client,
            Pack = content.Pack,
            Mapping = content.Mapping,
            Area = content.Area,
            Character = content.Character,
            CaptureStartedAt = content.Capture?.StartedAt,
            HasMessage = hasMessage,
            Files = [.. files.Select(file => new ReportFile(file.Name, file.Data.Length, Convert.ToHexStringLower(SHA256.HashData(file.Data))))],
        };

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var (name, data) in files)
                Add(zip, name, data);

            Add(zip, ReportFiles.Manifest, JsonSerializer.SerializeToUtf8Bytes(manifest, ReportJson.Default.ReportManifest));
        }

        return manifest;
    }

    private static void Add(ZipArchive zip, string name, byte[] data)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);

        using var stream = entry.Open();

        stream.Write(data);
    }
}
