using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace D3B.Extractor;

/// <summary>A data folder being written: its files, then its manifest, which the app reads first (docs/data.md).</summary>
/// <remarks>
/// Everything goes to a folder beside the target, which replaces the target only once complete, so the app never reads
/// a folder half written under the name it looks for.
/// </remarks>
internal sealed class DataFolder
{
    /// <summary>The format of the folders this extractor writes; <c>data::FORMAT</c> in the app must be the same.</summary>
    public const int Format = 1;

    /// <summary>The manifest's file name, as <c>Manifest::FILE</c> names it in the app.</summary>
    public const string ManifestFile = "manifest.json";

    // The manifest is written for people too: a version's `+` reads better unescaped, and no browser reads it as HTML.
    private static readonly JsonWriterOptions _jsonOptions = new() { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string _target;
    private readonly string _staging;
    private readonly SortedDictionary<string, string> _hashes = new(StringComparer.Ordinal);

    private DataFolder(string target)
    {
        _target = target;
        _staging = target + ".tmp";
    }

    /// <summary>Starts a data folder that will replace <paramref name="target"/> once complete.</summary>
    /// <exception cref="IOException"><paramref name="target"/> exists and is not a data folder.</exception>
    public static DataFolder Create(string target)
    {
        var folder = new DataFolder(Path.GetFullPath(target));

        folder.CheckReplaceable();

        if (Directory.Exists(folder._staging))
        {
            Directory.Delete(folder._staging, true);
        }

        Directory.CreateDirectory(folder._staging);

        return folder;
    }

    /// <summary>Writes one file of the folder, at <paramref name="path"/>, relative and with forward slashes.</summary>
    /// <exception cref="ArgumentException">The path is not a plain relative path, or was written already.</exception>
    public void Write(string path, ReadOnlySpan<byte> content)
    {
        var parts = path.Split('/');

        if (path == ManifestFile || parts.Any(part => part is "" or "." or ".." || part.Contains('\\', StringComparison.Ordinal) || part.Contains(':', StringComparison.Ordinal)))
        {
            throw new ArgumentException($"'{path}' is not a plain relative path of the data folder.", nameof(path));
        }

        if (_hashes.ContainsKey(path))
        {
            throw new ArgumentException($"'{path}' was written already.", nameof(path));
        }

        var file = Path.Combine([_staging, .. parts]);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, content);
        _hashes.Add(path, Convert.ToHexStringLower(SHA256.HashData(content)));
    }

    /// <summary>Writes the manifest, then puts the folder in place of the target.</summary>
    public void Complete(string build, string extractor)
    {
        File.WriteAllBytes(Path.Combine(_staging, ManifestFile), Manifest(build, extractor));

        CheckReplaceable();

        if (Directory.Exists(_target))
        {
            Directory.Delete(_target, true);
        }

        Directory.Move(_staging, _target);
    }

    private static string NameOf(string path)
    {
        var extension = Path.GetExtension(path);

        return extension.Length > 0 ? path[..^extension.Length] : path;
    }

    private byte[] Manifest(string build, string extractor)
    {
        using var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer, _jsonOptions))
        {
            json.WriteStartObject();
            json.WriteNumber("format", Format);
            json.WriteString("build", build);
            json.WriteString("extractor", extractor);
            json.WriteStartArray("tables");

            foreach (var (path, sha256) in _hashes)
            {
                json.WriteStartObject();
                json.WriteString("name", NameOf(path));
                json.WriteString("path", path);
                json.WriteString("sha256", sha256);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');

        return buffer.ToArray();
    }

    private void CheckReplaceable()
    {
        // The target is deleted before the new folder takes its place: only a folder this tool wrote may be.
        if (Directory.Exists(_target) && Directory.EnumerateFileSystemEntries(_target).Any() && !File.Exists(Path.Combine(_target, ManifestFile)))
        {
            throw new IOException($"{_target} is not a data folder: it has no {ManifestFile}, so it is not replaced.");
        }
    }
}
