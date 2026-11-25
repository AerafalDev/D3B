using System.Globalization;
using System.Text;
using D3B.Bot.Characters;

namespace D3B.Bot.Journal;

/// <summary>The journals kept on disk: a folder for each bot and one for the host, a file for each UTC day, one line an entry (D504).</summary>
/// <remarks>
/// <para>
/// The journal in memory keeps 2,000 entries (D113): after a night the cause of a stop has scrolled away, and a restart loses it.
/// What the host writes here outlives both, for the days and megabytes of <see cref="JournalRetention"/>. A day's file stops at
/// <see cref="MaxFileBytes"/>, saying so once, so that a bot writing in a loop cannot fill the disk before the next sweep.
/// </para>
/// <para>
/// Each write opens the file, appends and closes it, sharing it for reading and deleting: the app reads a journal while the host
/// writes it, and a sweep deletes a file between two writes. Lines are entries' records and the host's own messages (D112, D114):
/// they never hold a frame of the protocol, and so never the account's ticket.
/// </para>
/// </remarks>
public sealed class JournalFiles
{
    /// <summary>The setting holding the folder of the journals.</summary>
    public const string DirectoryKey = "Journal:Directory";

    /// <summary>The folder of the host's problems.</summary>
    public const string HostSource = "host";

    /// <summary>The bytes a day's file of one source holds at most, 20 MB.</summary>
    public const long MaxFileBytes = 20L * 1024 * 1024;

    private const string Extension = ".txt";
    private const string DayFormat = "yyyy-MM-dd";
    private const string CutText = " The journal of this day stops here: its file reached ";

    private static readonly UTF8Encoding _utf8 = new(false);

    private readonly long _maxFileBytes;

    /// <summary>Gets the folder of the journals by default: <c>%LOCALAPPDATA%\D3B\journals</c>.</summary>
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "journals");

    /// <summary>Gets the folder of the journals.</summary>
    public string Directory { get; }

    /// <summary>Initializes a new instance of the <see cref="JournalFiles"/> class.</summary>
    /// <param name="directory">The folder of the journals, made at the first write.</param>
    /// <param name="maxFileBytes">The bytes a day's file holds at most.</param>
    public JournalFiles(string directory, long maxFileBytes = MaxFileBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileBytes);

        Directory = directory;
        _maxFileBytes = maxFileBytes;
    }

    /// <summary>Names a bot's folder.</summary>
    /// <param name="key">The bot's character.</param>
    /// <returns>Its account and character, such as <c>3-11</c>: a name holds no character's name.</returns>
    public static string SourceOf(CharacterKey key)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{key.AccountId}-{key.CharacterId}");
    }

    // The cut line may leave a file under its cap, when the line that did not fit was longer: its last line tells.
    private static bool IsCut(FileStream stream)
    {
        var tail = (int)Math.Min(stream.Length, 256);

        if (tail is 0)
            return false;

        var bytes = new byte[tail];

        stream.Seek(-tail, SeekOrigin.End);
        stream.ReadExactly(bytes);

        var text = _utf8.GetString(bytes).TrimEnd('\n');

        return text[(text.LastIndexOf('\n') + 1)..].Contains(CutText, StringComparison.Ordinal);
    }

    /// <summary>Lists the sources that have a folder.</summary>
    /// <returns>The folders' names.</returns>
    public IReadOnlyList<string> Sources()
    {
        if (!System.IO.Directory.Exists(Directory))
            return [];

        return [.. System.IO.Directory.EnumerateDirectories(Directory).Select(Path.GetFileName).OfType<string>()];
    }

    /// <summary>Appends lines to a source's journal, each in its day's file.</summary>
    /// <param name="source">The source: <see cref="HostSource"/> or <see cref="SourceOf"/>.</param>
    /// <param name="lines">The lines, oldest first.</param>
    /// <exception cref="IOException">A file could not be written.</exception>
    public void Append(string source, IReadOnlyList<JournalLine> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count is 0)
            return;

        var folder = Path.Combine(Directory, source);

        System.IO.Directory.CreateDirectory(folder);

        foreach (var day in lines.GroupBy(line => line.At.UtcDateTime.Date))
        {
            var path = Path.Combine(folder, day.Key.ToString(DayFormat, CultureInfo.InvariantCulture) + Extension);

            using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

            // A file cut already says so on its last line: nothing more is written to it that day.
            if (stream.Length >= _maxFileBytes || IsCut(stream))
                continue;

            var text = new StringBuilder();
            var length = stream.Length;

            foreach (var line in day)
            {
                var bytes = _utf8.GetByteCount(line.Text) + 1;

                if (length + bytes > _maxFileBytes)
                {
                    text.Append(JournalText.TimeOf(line.At)).Append(CutText).Append(_maxFileBytes / 1024 / 1024).Append(" MB.\n");
                    break;
                }

                text.Append(line.Text).Append('\n');
                length += bytes;
            }

            stream.Seek(0, SeekOrigin.End);
            stream.Write(_utf8.GetBytes(text.ToString()));
        }
    }

    /// <summary>Reads a source's journal, oldest first.</summary>
    /// <param name="source">The source.</param>
    /// <param name="since">The time before which lines are left out, or null for every line kept.</param>
    /// <param name="maxBytes">The bytes read at most, the newest lines kept whole, or null for no limit.</param>
    /// <returns>The lines, each ending with a line break; empty when the source has none.</returns>
    /// <exception cref="IOException">A file could not be read.</exception>
    public string Read(string source, DateTimeOffset? since = null, long? maxBytes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var folder = Path.Combine(Directory, source);

        if (!System.IO.Directory.Exists(folder))
            return string.Empty;

        var firstDay = since?.UtcDateTime.Date.ToString(DayFormat, CultureInfo.InvariantCulture);
        var days = System.IO.Directory.EnumerateFiles(folder, "*" + Extension)
            .Select(path => (Path: path, Day: Path.GetFileNameWithoutExtension(path)))
            .Where(file => file.Day.Length == DayFormat.Length && (firstDay is null || string.CompareOrdinal(file.Day, firstDay) >= 0))
            .OrderBy(file => file.Day, StringComparer.Ordinal);
        var lines = new List<string>();

        foreach (var (path, _) in days)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, _utf8);

            while (reader.ReadLine() is { } line)
            {
                if (since is { } from && JournalText.TryReadTime(line, out var at) && at < from)
                    continue;

                lines.Add(line);
            }
        }

        var first = 0;

        if (maxBytes is { } most)
        {
            var total = 0L;

            for (first = lines.Count; first > 0; first--)
            {
                var bytes = _utf8.GetByteCount(lines[first - 1]) + 1;

                if (total + bytes > most)
                    break;

                total += bytes;
            }
        }

        var text = new StringBuilder();

        for (var index = first; index < lines.Count; index++)
            text.Append(lines[index]).Append('\n');

        return text.ToString();
    }

}
