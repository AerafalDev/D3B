using System.Text;
using System.Text.Json;
using D3B.Bot.Journal;
using D3B.Bot.Runtime;

namespace D3B.Bot.Reports;

/// <summary>The texts of a report: the journal and the trace of the character, one line an entry, and the settings that differ (D410, D504).</summary>
/// <remarks>
/// The journal is read from the journals on disk, the last day of it (D504); the trace is in memory only, 1,000 decisions (D350), and a
/// report snapshots it. A line is the entry as its record prints it, in no language and with every field (<see cref="JournalText"/>),
/// since the maintainer reads it and the app's wording is for the player. The character's name is its number.
/// </remarks>
public static class ReportTexts
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    /// <summary>Writes a character's journal, as it is in memory.</summary>
    /// <param name="entries">The entries, oldest first.</param>
    /// <param name="identity">Who the text must not name.</param>
    /// <returns>One line an entry, from its time.</returns>
    public static string Journal(IEnumerable<JournalEntry> entries, ReportIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return Lines(entries.Select(entry => JournalText.Of(entry).Text), identity);
    }

    /// <summary>Writes a journal read from disk, a character's or the host's.</summary>
    /// <param name="lines">The lines, as <see cref="JournalFiles.Read"/> gives them.</param>
    /// <param name="identity">Who the text must not name.</param>
    /// <returns>The same lines, naming nobody.</returns>
    public static string Journal(string lines, ReportIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return Lines(lines.Split('\n', StringSplitOptions.RemoveEmptyEntries), identity);
    }

    /// <summary>Writes a character's decisions.</summary>
    /// <param name="entries">The decisions, oldest first.</param>
    /// <param name="identity">Who the text must not name.</param>
    /// <returns>One line a decision, from its time.</returns>
    public static string Trace(IEnumerable<TraceEntry> entries, ReportIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return Lines(entries.Select(entry => JournalText.Of(entry).Text), identity);
    }

    /// <summary>Writes the settings the player changed, by their configuration's keys.</summary>
    /// <param name="settings">The settings; one with no value is the default and is left out.</param>
    /// <returns>JSON, sorted by key.</returns>
    public static string Settings(IReadOnlyDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return JsonSerializer.Serialize(settings.Where(pair => pair.Value is not null).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value), _json);
    }

    private static string Lines(IEnumerable<string> lines, ReportIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var text = new StringBuilder();

        foreach (var line in lines)
            text.Append(identity.Hide(line.TrimEnd('\r'))).Append('\n');

        return text.ToString();
    }
}
