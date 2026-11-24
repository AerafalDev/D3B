using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Journal;

/// <summary>How long and how much of the journals on disk D3B keeps, which the host applies as it starts and at each new day (D504).</summary>
/// <remarks>
/// The owner's numbers: seven days, as the captures (D409), and 250 MB, which a day of ten bots playing round the clock, about
/// 36 MB, leaves room for. Only a journal's day files are deleted, the oldest first, whatever else the folder holds.
/// </remarks>
public sealed partial record JournalRetention
{
    /// <summary>The setting holding the days a journal's file is kept; 0 keeps it whatever its age.</summary>
    public const string DaysKey = "Journal:Retention:Days";

    /// <summary>The setting holding the megabytes the journals may take together; 0 sets no limit.</summary>
    public const string MegabytesKey = "Journal:Retention:Megabytes";

    /// <summary>The most days a journal can be kept, ten years.</summary>
    public const long MostDays = 3650;

    /// <summary>The most megabytes the journals can take, a terabyte.</summary>
    public const long MostMegabytes = 1_048_576;

    private const long DefaultDays = 7;
    private const long DefaultMegabytes = 250;
    private const long BytesInAMegabyte = 1024 * 1024;

    /// <summary>Gets the retention by default: seven days and 250 MB.</summary>
    public static JournalRetention Default { get; } = new() { MaxAge = TimeSpan.FromDays(DefaultDays), MaxBytes = DefaultMegabytes * BytesInAMegabyte };

    /// <summary>Gets how long a file is kept after its last line, or null to keep it whatever its age.</summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>Gets how many bytes the journals may take together, or null for no limit.</summary>
    public long? MaxBytes { get; init; }

    /// <summary>Reads a retention from a configuration, with the default for what it does not set.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The retention.</returns>
    /// <exception cref="InvalidOperationException">A setting is not a whole number of at least zero.</exception>
    public static JournalRetention Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var days = ReadWhole(configuration, DaysKey, DefaultDays, MostDays);
        var megabytes = ReadWhole(configuration, MegabytesKey, DefaultMegabytes, MostMegabytes);

        return new JournalRetention
        {
            MaxAge = days is 0 ? null : TimeSpan.FromDays(days),
            MaxBytes = megabytes is 0 ? null : megabytes * BytesInAMegabyte,
        };
    }

    /// <summary>Deletes the journals' files that are too old, then the oldest ones while the rest is over the size.</summary>
    /// <param name="directory">The folder of the journals, a folder for each source. A missing one holds nothing.</param>
    /// <param name="time">The clock that tells how old a file is.</param>
    /// <returns>How many files were deleted.</returns>
    public int Sweep(string directory, TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(time);

        if (!Directory.Exists(directory))
            return 0;

        var now = time.GetUtcNow();
        var kept = new List<FileInfo>();
        var deleted = 0;

        var files = new DirectoryInfo(directory).EnumerateDirectories()
            .SelectMany(folder => folder.EnumerateFiles("*.txt"))
            .Where(file => DayFile().IsMatch(file.Name))
            .OrderBy(file => file.LastWriteTimeUtc);

        foreach (var file in files)
        {
            if (MaxAge is { } age && now - file.LastWriteTimeUtc > age && TryDelete(file))
                deleted++;
            else
                kept.Add(file);
        }

        if (MaxBytes is not { } limit)
            return deleted;

        var total = kept.Sum(file => file.Length);

        // Oldest first, the order of the list; a file that cannot be deleted stays in the total.
        foreach (var file in kept)
        {
            if (total <= limit)
                break;

            var length = file.Length;

            if (TryDelete(file))
            {
                total -= length;
                deleted++;
            }
        }

        return deleted;
    }

    private static long ReadWhole(IConfiguration configuration, string key, long fallback, long most)
    {
        if (configuration[key] is not { Length: > 0 } text)
            return fallback;

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value > most)
            throw new InvalidOperationException($"{key} is '{text}', which is not a whole number from 0 to {most}.");

        return value;
    }

    private static bool TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}\.txt\z", RegexOptions.CultureInvariant)]
    private static partial Regex DayFile();
}
