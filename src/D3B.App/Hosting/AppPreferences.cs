using System.Text.Json;

namespace D3B.App.Hosting;

/// <summary>What the app remembers of its own: its language and theme, in a file of its own beside the database (D101).</summary>
/// <remarks>
/// Read before the window shows, while the host, and its database, still get ready: the window opens in the language and
/// theme it was left in. A file that does not read, or cannot be written, leaves the app on its defaults, the system's
/// language and dark.
/// </remarks>
internal sealed class AppPreferences
{
    /// <summary>The theme that is not the default.</summary>
    public const string Light = "Light";

    /// <summary>The default theme.</summary>
    public const string Dark = "Dark";

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    /// <summary>Gets the file by default: <c>%LOCALAPPDATA%\D3B\app.json</c>.</summary>
    public static string DefaultPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "app.json");

    /// <summary>Gets the file.</summary>
    public string Path { get; }

    /// <summary>Gets the language the app was left in, <c>fr</c> or <c>en</c>, if it was changed.</summary>
    public string? Language { get; private set; }

    /// <summary>Gets the theme the app was left in, if it was changed.</summary>
    public string? Theme { get; private set; }

    private AppPreferences(string path, string? language, string? theme)
    {
        Path = path;
        Language = language;
        Theme = theme;
    }

    /// <summary>Reads what the app remembers.</summary>
    /// <param name="path">The file.</param>
    /// <returns>What it remembers, or nothing when the file is missing or does not read.</returns>
    public static AppPreferences Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));

            return new AppPreferences(path, stored?.Language, stored?.Theme);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppPreferences(path, null, null);
        }
    }

    /// <summary>Remembers the language the app is in.</summary>
    /// <param name="language"><c>fr</c> or <c>en</c>.</param>
    public void RememberLanguage(string language)
    {
        Language = language;
        Save();
    }

    /// <summary>Remembers the theme the app is in.</summary>
    /// <param name="theme"><see cref="Dark"/> or <see cref="Light"/>.</param>
    public void RememberTheme(string theme)
    {
        Theme = theme;
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(new Stored(Language, Theme), _json));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Forgetting the theme does no harm: the app goes on.
        }
    }

    private sealed record Stored(string? Language, string? Theme);
}
