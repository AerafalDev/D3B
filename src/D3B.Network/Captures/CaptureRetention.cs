using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace D3B.Network.Captures;

/// <summary>How long and how much of the game's captures D3B keeps, which the host applies at each start (D409).</summary>
/// <remarks>
/// A capture holds the account's login ticket and a whole session of the game: it is kept to be replayed or reported
/// (D410), and no longer than that. A capture still being written is never deleted, whatever its age.
/// </remarks>
public sealed record CaptureRetention
{
    private const long BytesInAMegabyte = 1024 * 1024;

    /// <summary>Gets the retention by default: seven days and a gigabyte.</summary>
    public static CaptureRetention Default { get; } = new() { MaxAge = TimeSpan.FromDays(7), MaxBytes = 1024 * BytesInAMegabyte };

    /// <summary>Gets how long a capture is kept after its last record, or null to keep it whatever its age.</summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>Gets how many bytes the captures of a directory may take together, or null for no limit.</summary>
    public long? MaxBytes { get; init; }

    /// <summary>Reads a retention from a configuration, with the default for what it does not set.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="daysKey">The setting holding the days; 0 keeps captures whatever their age.</param>
    /// <param name="megabytesKey">The setting holding the megabytes; 0 sets no limit.</param>
    /// <returns>The retention.</returns>
    /// <exception cref="InvalidOperationException">A setting is not a whole number of at least zero.</exception>
    public static CaptureRetention Read(IConfiguration configuration, string daysKey, string megabytesKey)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var days = ReadWhole(configuration, daysKey, 7);
        var megabytes = ReadWhole(configuration, megabytesKey, 1024);

        return new CaptureRetention
        {
            MaxAge = days is 0 ? null : TimeSpan.FromDays(days),
            MaxBytes = megabytes is 0 ? null : megabytes * BytesInAMegabyte,
        };
    }

    /// <summary>Deletes the captures of a directory that are too old, then the oldest ones while the rest is over the size.</summary>
    /// <param name="directory">The directory; its subdirectories are left alone. A missing one holds nothing.</param>
    /// <param name="time">The clock that tells how old a capture is.</param>
    /// <returns>What was deleted and what is left.</returns>
    public CaptureSweep Sweep(string directory, TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(time);

        if (!Directory.Exists(directory))
            return default;

        var now = time.GetUtcNow();
        var kept = new List<FileInfo>();
        var deleted = 0;
        var deletedBytes = 0L;

        foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*" + CaptureWriter.FileExtension).OrderBy(file => file.LastWriteTimeUtc))
        {
            var length = file.Length;

            if (MaxAge is { } age && now - file.LastWriteTimeUtc > age && TryDelete(file))
            {
                deleted++;
                deletedBytes += length;
            }
            else
            {
                kept.Add(file);
            }
        }

        var total = kept.Sum(file => file.Length);

        if (MaxBytes is { } limit)
        {
            // Oldest first, the order of the list; a file that cannot be deleted stays in the total.
            foreach (var file in kept.ToList())
            {
                if (total <= limit)
                    break;

                var length = file.Length;

                if (TryDelete(file))
                {
                    kept.Remove(file);
                    total -= length;
                    deleted++;
                    deletedBytes += length;
                }
            }
        }

        return new CaptureSweep(deleted, deletedBytes, kept.Count, total);
    }

    private static long ReadWhole(IConfiguration configuration, string key, long fallback)
    {
        if (configuration[key] is not { Length: > 0 } text)
            return fallback;

        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException($"{key} is '{text}', which is not a whole number of at least zero.");

        return value;
    }

    private static bool TryDelete(FileInfo file)
    {
        try
        {
            // Opened to nobody else and deleted as it closes: the writer of a capture still recording holds it open for
            // writing, so that this open is refused and the capture stays, on any system.
            using (new FileStream(file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
