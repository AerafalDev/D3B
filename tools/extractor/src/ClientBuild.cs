using System.Globalization;

namespace D3B.Extractor;

/// <summary>A build of the official client, as its <c>Dofus_Data/StreamingAssets/version</c> file names it.</summary>
/// <remarks>
/// The version names the data folder, its release and the asset's URL, so only digits and dots are accepted: a crafted
/// file cannot point the output outside its folder.
/// </remarks>
internal sealed class ClientBuild
{
    private const int MaxLength = 64;

    // The version file is three short lines (113 bytes on 3.7.13.17): anything larger is not one.
    private const int MaxFileSize = 4096;

    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>Gets the client's version, such as <c>3.7.13.17</c>.</summary>
    public string Version { get; }

    /// <summary>Gets when the client was built, when its version file says so.</summary>
    public DateTimeOffset? BuildDate { get; }

    private ClientBuild(string version, DateTimeOffset? buildDate)
    {
        Version = version;
        BuildDate = buildDate;
    }

    /// <summary>Reads the build of an installed client.</summary>
    /// <exception cref="InvalidDataException">The version file is missing or names no valid version.</exception>
    public static ClientBuild Read(ClientInstall client)
    {
        var path = Path.Combine(client.StreamingAssets, "version");

        if (!File.Exists(path))
        {
            throw new InvalidDataException($"The client has no version file at {path}.");
        }

        if (new FileInfo(path).Length > MaxFileSize)
        {
            throw new InvalidDataException($"{path} is over {MaxFileSize} bytes: it is not a version file.");
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>Parses a version file: <c>Key=Value</c> lines, among them <c>Version</c> and <c>BuildDate</c> (docs/data.md).</summary>
    /// <exception cref="InvalidDataException">The text names no valid version, an unreadable build date, or a key twice.</exception>
    public static ClientBuild Parse(string text)
    {
        string? version = null;
        DateTimeOffset? buildDate = null;
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (!keys.Add(key))
            {
                throw new InvalidDataException($"The client's version file names {key} twice.");
            }

            if (key == "Version")
            {
                version = value;
            }
            else if (key == "BuildDate")
            {
                if (!DateTimeOffset.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    throw new InvalidDataException($"The client's build date '{value}' is not a date.");
                }

                buildDate = date;
            }
        }

        if (version is null)
        {
            throw new InvalidDataException("The client's version file has no Version line.");
        }

        if (!IsVersion(version))
        {
            throw new InvalidDataException($"The client's version '{version}' is not numbers separated by dots.");
        }

        return new ClientBuild(version, buildDate);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return BuildDate is { } date ? $"{Version}, built {date:u}" : Version;
    }

    private static bool IsVersion(string value)
    {
        if (value.Length is 0 or > MaxLength)
        {
            return false;
        }

        return value.Split('.').All(part => part.Length > 0 && part.All(char.IsAsciiDigit));
    }
}
