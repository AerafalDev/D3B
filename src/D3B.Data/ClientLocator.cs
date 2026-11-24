using System.Text.Json;

namespace D3B.Data;

/// <summary>Finds the official client's installation directory: the one named, else the launcher's, else its default.</summary>
/// <remarks>
/// The Ankama launcher lets the player install a game on any drive and records where in its own release file; it was
/// measured on 3.7.14.18 (docs/client.md, "Installation"). A location there that holds no client, as after a move by hand,
/// is passed over rather than trusted.
/// </remarks>
public static class ClientLocator
{
    /// <summary>The key of the host's configuration that names the client's directory, which the app's settings set too.</summary>
    public const string ConfigurationKey = "Client:Directory";

    /// <summary>Gets where the launcher installs the client when the player keeps its choice.</summary>
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ankama", "Dofus-dofus3");

    /// <summary>Gets the launcher's file that names where it installed Dofus 3.</summary>
    public static string LauncherReleaseFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zaap", "repositories", "production", "dofus", "dofus3", "release.json");

    /// <summary>Gets the client's directory: the one named, else where the launcher installed it, else its default.</summary>
    /// <param name="configured">The directory the player named, if any.</param>
    /// <returns>The directory, which may hold no client when the one named or the default does not.</returns>
    public static string Locate(string? configured)
    {
        return Locate(configured, LauncherReleaseFile, DefaultDirectory);
    }

    /// <summary>Gets the client's directory: the one named, else where a release file says, else a default.</summary>
    /// <param name="configured">The directory the player named, if any.</param>
    /// <param name="releaseFile">The launcher's release file.</param>
    /// <param name="defaultDirectory">The directory used when neither names one that holds a client.</param>
    /// <returns>The directory.</returns>
    public static string Locate(string? configured, string releaseFile, string defaultDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return FromLauncher(releaseFile) is { } location && HoldsClient(location) ? location : defaultDirectory;
    }

    /// <summary>Reads where the launcher installed the client.</summary>
    /// <param name="releaseFile">The launcher's release file.</param>
    /// <returns>Its <c>location</c>; or null when the file is missing, unreadable or names none.</returns>
    public static string? FromLauncher(string releaseFile)
    {
        try
        {
            using var stream = File.OpenRead(releaseFile);
            using var document = JsonDocument.Parse(stream);

            return document.RootElement.ValueKind is JsonValueKind.Object
                && document.RootElement.TryGetProperty("location", out var location)
                && location.ValueKind is JsonValueKind.String
                && location.GetString() is { Length: > 0 } path
                    ? path
                    : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // The launcher's file is its own, not a contract: what cannot be read leaves the default to be tried.
            return null;
        }
    }

    /// <summary>Tells whether a directory holds an installed client: the version file its build is read from.</summary>
    /// <param name="directory">The directory.</param>
    /// <returns>True when it holds one.</returns>
    public static bool HoldsClient(string directory)
    {
        return File.Exists(Path.Combine(directory, "Dofus_Data", "StreamingAssets", "version"));
    }
}
