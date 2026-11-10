using System.Globalization;
using System.Text;
using System.Text.Json;
using D3B.Bot.Journal;
using D3B.Bot.Runtime;

namespace D3B.Bot.Reports;

/// <summary>The texts of a report: the journal and the trace of the character, one line an entry, and the settings that differ (D410).</summary>
/// <remarks>
/// Both are in memory only, 2,000 entries and 1,000 decisions (D112, D350): a report snapshots them. A line is the entry as its record
/// prints it, in no language and with every field, since the maintainer reads it and the app's wording is for the player. The character's name is its number.
/// </remarks>
public static class ReportTexts
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    /// <summary>Writes a character's journal.</summary>
    /// <param name="entries">The entries, oldest first.</param>
    /// <param name="identity">Who the text must not name.</param>
    /// <returns>One line an entry, from its time.</returns>
    public static string Journal(IEnumerable<JournalEntry> entries, ReportIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return Lines(entries.Select(entry => (entry.At, Text: $"{new string(' ', 2 * entry.Depth)}{entry}")), identity);
    }

    /// <summary>Writes a character's decisions.</summary>
    /// <param name="entries">The decisions, oldest first.</param>
    /// <param name="identity">Who the text must not name.</param>
    /// <returns>One line a decision, from its time.</returns>
    public static string Trace(IEnumerable<TraceEntry> entries, ReportIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return Lines(entries.Select(entry => (entry.At, Text: entry.ToString())), identity);
    }

    /// <summary>Writes the settings the player changed, by their configuration's keys.</summary>
    /// <param name="settings">The settings; one with no value is the default and is left out.</param>
    /// <returns>JSON, sorted by key.</returns>
    public static string Settings(IReadOnlyDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return JsonSerializer.Serialize(settings.Where(pair => pair.Value is not null).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value), _json);
    }

    private static string Lines(IEnumerable<(DateTimeOffset At, string Text)> entries, ReportIdentity identity)
    {
        var text = new StringBuilder();

        foreach (var (at, line) in entries)
        {
            var oneLine = line.ReplaceLineEndings(" ");

            text.Append(at.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture)).Append(' ').Append(identity.Hide(oneLine)).Append('\n');
        }

        return text.ToString();
    }
}
