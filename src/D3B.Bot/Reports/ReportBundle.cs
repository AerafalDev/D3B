using System.Text;
using System.Text.Json;

namespace D3B.Bot.Reports;

/// <summary>A report read and checked: its manifest and its files, every one of the size and SHA-256 the manifest lists.</summary>
public sealed class ReportBundle
{
    internal ReportBundle(ReportManifest manifest, IReadOnlyDictionary<string, byte[]> files)
    {
        Manifest = manifest;
        Files = files;
    }

    /// <summary>Gets what the report says of itself.</summary>
    public ReportManifest Manifest { get; }

    /// <summary>Gets every file but the manifest, by name.</summary>
    public IReadOnlyDictionary<string, byte[]> Files { get; }

    /// <summary>Gets the player's message as text, or null when there is none.</summary>
    public string? Message => Files.TryGetValue(ReportFiles.Message, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;

    /// <summary>Writes the files in a folder, the manifest among them; their names were checked, so that none leaves it.</summary>
    /// <param name="directory">The folder, made when missing.</param>
    public void WriteTo(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ReportFiles.Manifest), JsonSerializer.Serialize(Manifest, ReportJson.Options));

        foreach (var (name, bytes) in Files)
        {
            var path = Path.Combine(directory, name.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
    }
}
