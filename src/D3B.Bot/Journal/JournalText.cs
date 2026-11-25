using System.Globalization;
using D3B.Bot.Hosting;
using D3B.Bot.Runtime;

namespace D3B.Bot.Journal;

/// <summary>The lines a journal is written as, on disk and in a report: one an entry or a problem, from its UTC time, in no language (D410, D504).</summary>
/// <remarks>
/// A line is the entry as its record prints it, with every field, since the maintainer reads it and the app's wording is for the
/// player. A record prints its fields in the thread's culture: the line is written in the invariant one, so that a journal reads the
/// same on every machine. A line break in a field becomes a space, so that each line is one entry, which a time can be read from.
/// </remarks>
public static class JournalText
{
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    // What the format writes: 2026-11-24T20:05:03.120Z.
    private const int TimeLength = 24;

    /// <summary>Writes a time as a line starts with it.</summary>
    /// <param name="at">The time.</param>
    /// <returns>The UTC time to the millisecond, such as <c>2026-11-24T20:05:03.120Z</c>.</returns>
    public static string TimeOf(DateTimeOffset at)
    {
        return at.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>Reads the time a line starts with.</summary>
    /// <param name="line">The line.</param>
    /// <param name="at">The time, when there is one.</param>
    /// <returns>Whether the line starts with a time.</returns>
    public static bool TryReadTime(ReadOnlySpan<char> line, out DateTimeOffset at)
    {
        if (line.Length >= TimeLength && DateTime.TryParseExact(line[..TimeLength], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time))
        {
            at = new DateTimeOffset(time, TimeSpan.Zero);

            return true;
        }

        at = default;

        return false;
    }

    /// <summary>Writes an entry of a bot's journal.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>Its line, indented by its depth.</returns>
    public static JournalLine Of(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new JournalLine(entry.At, $"{TimeOf(entry.At)} {new string(' ', 2 * entry.Depth)}{Invariant(entry)}".ReplaceLineEndings(" "));
    }

    /// <summary>Writes a decision of a bot's trace.</summary>
    /// <param name="entry">The decision.</param>
    /// <returns>Its line.</returns>
    public static JournalLine Of(TraceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new JournalLine(entry.At, $"{TimeOf(entry.At)} {Invariant(entry)}".ReplaceLineEndings(" "));
    }

    /// <summary>Writes a problem of the host.</summary>
    /// <param name="problem">The problem.</param>
    /// <returns>Its line: its level, its log, its message, and its exception after a bar.</returns>
    public static JournalLine Of(HostProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var text = $"{TimeOf(problem.At)} {problem.Level} {problem.Category}: {problem.Message}";

        if (problem.Exception is { } exception)
            text += " | " + exception;

        return new JournalLine(problem.At, text.ReplaceLineEndings(" "));
    }

    private static string Invariant(object value)
    {
        var culture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            return value.ToString() ?? string.Empty;
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
