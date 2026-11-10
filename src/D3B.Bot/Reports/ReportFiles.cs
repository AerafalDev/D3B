using System.Text.RegularExpressions;

namespace D3B.Bot.Reports;

/// <summary>The names of the files a report holds (D410, D411).</summary>
public static partial class ReportFiles
{
    /// <summary>The manifest, listing every other file with its size and SHA-256.</summary>
    public const string Manifest = "manifest.json";

    /// <summary>The player's own message, if they wrote one.</summary>
    public const string Message = "message.md";

    /// <summary>The capture of the connection concerned, cleaned.</summary>
    public const string Capture = "capture.d3bcap";

    /// <summary>The character's journal, as text.</summary>
    public const string Journal = "journal.txt";

    /// <summary>The character's decisions, as text.</summary>
    public const string Trace = "trace.txt";

    /// <summary>The settings that differ from the defaults.</summary>
    public const string Settings = "settings.json";

    /// <summary>What the bots learned, by ids.</summary>
    public const string Knowledge = "knowledge.json";

    /// <summary>The folder of the assets the character was playing: its profile, its routes and zones.</summary>
    public const string AssetsFolder = "assets/";

    private static readonly HashSet<string> _known = new(StringComparer.Ordinal) { Manifest, Message, Capture, Journal, Trace, Settings, Knowledge };

    /// <summary>Tells whether a name can be a file of a report: a name of the list above, or a file of the assets' folder. A name with a separator, a dot-dot or a drive is none, so that unpacking one never leaves its folder.</summary>
    /// <param name="name">The name in the bundle.</param>
    /// <returns>Whether it is acceptable.</returns>
    public static bool IsValid(string? name)
    {
        return name is not null && (_known.Contains(name) || AssetPattern().IsMatch(name));
    }

    [GeneratedRegex(@"^assets/[A-Za-z0-9][A-Za-z0-9._-]{0,99}\.json\z", RegexOptions.CultureInvariant)]
    private static partial Regex AssetPattern();
}
