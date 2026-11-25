using System.Text;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Data.Tests;

namespace D3B.Bot.Tests.Journal;

public sealed class JournalFilesTests : IDisposable
{
    private static readonly DateTimeOffset _morning = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static JournalLine Line(DateTimeOffset at, string text)
    {
        return new JournalLine(at, $"{JournalText.TimeOf(at)} {text}");
    }

    [Fact]
    public void ABotsLinesGoToAFileOfItsOwnForEachDayAndAreReadBackOldestFirst()
    {
        var files = new JournalFiles(_directory.Path);
        var source = JournalFiles.SourceOf(new CharacterKey(3, 11));

        files.Append(source, [Line(_morning.AddDays(-1), "yesterday"), Line(_morning, "this morning")]);
        files.Append(source, [Line(_morning.AddHours(2), "later")]);

        Assert.Equal("3-11", source);
        Assert.True(File.Exists(_directory.Combine("3-11", "2026-11-23.txt")));
        Assert.True(File.Exists(_directory.Combine("3-11", "2026-11-24.txt")));

        var lines = files.Read(source).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(["2026-11-23T08:00:00.000Z yesterday", "2026-11-24T08:00:00.000Z this morning", "2026-11-24T10:00:00.000Z later"], lines);
    }

    [Fact]
    public void ReadingSinceATimeLeavesOutTheLinesBeforeItWhateverTheirDay()
    {
        var files = new JournalFiles(_directory.Path);

        files.Append(JournalFiles.HostSource, [Line(_morning.AddDays(-2), "two days ago"), Line(_morning.AddHours(-20), "last night"), Line(_morning, "now")]);

        var text = files.Read(JournalFiles.HostSource, since: _morning.AddHours(-24));

        Assert.Equal("2026-11-23T12:00:00.000Z last night\n2026-11-24T08:00:00.000Z now\n", text);
    }

    [Fact]
    public void ReadingAtMostSomeBytesKeepsTheNewestLinesWhole()
    {
        var files = new JournalFiles(_directory.Path);

        files.Append("1-1", [Line(_morning, "first"), Line(_morning.AddSeconds(1), "second"), Line(_morning.AddSeconds(2), "third")]);

        var text = files.Read("1-1", maxBytes: 64);

        Assert.Equal("2026-11-24T08:00:01.000Z second\n2026-11-24T08:00:02.000Z third\n", text);
    }

    [Fact]
    public void ASourceWithNoFileReadsAsNothing()
    {
        var files = new JournalFiles(_directory.Path);

        Assert.Equal("", files.Read("9-9"));
        Assert.Empty(files.Sources());
    }

    [Fact]
    public void ADaysFileStopsAtItsCapAndSaysSoOnce()
    {
        var files = new JournalFiles(_directory.Path, maxFileBytes: 100);
        var many = Enumerable.Range(0, 10).Select(second => Line(_morning.AddSeconds(second), "a line of the journal")).ToList();

        files.Append("1-1", many);
        files.Append("1-1", many);

        var lines = files.Read("1-1").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Contains("stops here", lines[^1], StringComparison.Ordinal);
        Assert.True(new FileInfo(_directory.Combine("1-1", "2026-11-24.txt")).Length < 100 + 200);
    }

    [Fact]
    public void AFileCutUnderItsCapByALongLineTakesNoMoreLinesThatDay()
    {
        var files = new JournalFiles(_directory.Path, maxFileBytes: 200);

        files.Append("1-1", [Line(_morning, "short"), Line(_morning.AddSeconds(1), new string('x', 300))]);
        files.Append("1-1", [Line(_morning.AddSeconds(2), "after")]);

        var lines = files.Read("1-1").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.True(new FileInfo(_directory.Combine("1-1", "2026-11-24.txt")).Length < 200);
        Assert.Equal(2, lines.Length);
        Assert.Contains("stops here", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void TheSourcesAreTheFoldersWritten()
    {
        var files = new JournalFiles(_directory.Path);

        files.Append(JournalFiles.HostSource, [Line(_morning, "host")]);
        files.Append("3-11", [Line(_morning, "bot")]);

        Assert.Equal(["3-11", "host"], files.Sources().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AFileIsReadWhileItIsWritten()
    {
        var files = new JournalFiles(_directory.Path);

        files.Append("1-1", [Line(_morning, "first")]);

        using (new FileStream(_directory.Combine("1-1", "2026-11-24.txt"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            Assert.Equal("2026-11-24T08:00:00.000Z first\n", files.Read("1-1"));

        Assert.Equal(Encoding.UTF8.GetByteCount("2026-11-24T08:00:00.000Z first\n"), new FileInfo(_directory.Combine("1-1", "2026-11-24.txt")).Length);
    }
}
