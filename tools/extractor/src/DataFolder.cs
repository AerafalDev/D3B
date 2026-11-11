using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace D3B.Extractor;

/// <summary>A data folder being written: its files, then its manifest, which the app reads first (docs/data.md).</summary>
/// <remarks>
/// Everything goes to a staging folder beside the target. Once complete, the target is renamed aside, the staging folder
/// takes its name, and only then is the old one deleted: the app never finds a folder half written, or half deleted,
/// under the name it looks for.
/// </remarks>
internal sealed class DataFolder
{
    /// <summary>The format of the folders this extractor writes; <c>data::FORMAT</c> in the app must be the same.</summary>
    public const int Format = 1;

    /// <summary>The manifest's file name, as <c>Manifest::FILE</c> names it in the app.</summary>
    public const string ManifestFile = "manifest.json";

    // Names no one else gives a folder, so that clearing them never deletes someone's own work.
    private const string StagingSuffix = ".d3b-staging";
    private const string OldSuffix = ".d3b-old";

    // The manifest is written for people too: a version's `+` reads better unescaped, and no browser reads it as HTML.
    private static readonly JsonWriterOptions _jsonOptions = new() { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string _target;
    private readonly string _staging;
    private readonly string _old;
    private readonly SortedDictionary<string, string> _hashes = new(StringComparer.Ordinal);

    // The folder's names and paths are unique ignoring case: Windows and macOS would put two of them in one file.
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

    private DataFolder(string target)
    {
        _target = target;
        _staging = target + StagingSuffix;
        _old = target + OldSuffix;
    }

    /// <summary>Starts a data folder that will replace <paramref name="target"/> once complete.</summary>
    /// <exception cref="IOException"><paramref name="target"/> exists and is not a data folder.</exception>
    public static DataFolder Create(string target)
    {
        // A trailing separator would put the staging folder inside the target.
        var folder = new DataFolder(Path.TrimEndingDirectorySeparator(Path.GetFullPath(target)));

        folder.CheckReplaceable();
        DeleteIfExists(folder._staging);
        DeleteIfExists(folder._old);
        Directory.CreateDirectory(folder._staging);

        return folder;
    }

    /// <summary>Writes one file of the folder, at <paramref name="path"/>, relative and with forward slashes.</summary>
    /// <exception cref="ArgumentException">The path is not a plain relative path, or its path or name is taken.</exception>
    public void Write(string path, ReadOnlySpan<byte> content)
    {
        var parts = path.Split('/');

        if (path == ManifestFile || parts.Any(part => !IsPlainName(part)))
        {
            throw new ArgumentException($"'{path}' is not a plain relative path of the data folder.", nameof(path));
        }

        if (_paths.Contains(path) || _names.Contains(NameOf(path)))
        {
            throw new ArgumentException($"'{path}' or its name '{NameOf(path)}' was written already.", nameof(path));
        }

        var file = Path.Combine([_staging, .. parts]);

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, content);
        _paths.Add(path);
        _names.Add(NameOf(path));
        _hashes.Add(path, Convert.ToHexStringLower(SHA256.HashData(content)));
    }

    /// <summary>Writes the manifest, then puts the folder in place of the target.</summary>
    /// <exception cref="IOException">The target became something other than a data folder meanwhile.</exception>
    public void Complete(string build, string extractor)
    {
        File.WriteAllBytes(Path.Combine(_staging, ManifestFile), Manifest(build, extractor));

        CheckReplaceable();

        if (Directory.Exists(_target))
        {
            Directory.Move(_target, _old);
        }

        Directory.Move(_staging, _target);
        DeleteIfExists(_old);
    }

    private static string NameOf(string path)
    {
        var extension = Path.GetExtension(path);

        return extension.Length > 0 ? path[..^extension.Length] : path;
    }

    private static bool IsPlainName(string part)
    {
        // Windows drops a trailing dot or space, so "maps." and "maps" would be one folder there and two elsewhere.
        return part is not ("" or "." or "..")
            && !part.EndsWith('.')
            && !part.EndsWith(' ')
            && part.IndexOfAny(['\\', ':', '*', '?', '"', '<', '>', '|']) < 0;
    }

    private static void DeleteIfExists(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
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
        // The target is deleted once the new folder takes its place: only a folder this tool wrote may be.
        if (Directory.Exists(_target) && Directory.EnumerateFileSystemEntries(_target).Any() && !File.Exists(Path.Combine(_target, ManifestFile)))
        {
            throw new IOException($"{_target} is not a data folder: it has no {ManifestFile}, so it is not replaced.");
        }
    }
}
