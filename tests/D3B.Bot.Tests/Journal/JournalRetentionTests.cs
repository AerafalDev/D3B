using D3B.Bot.Journal;
using D3B.Data.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Journal;

public sealed class JournalRetentionTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory _directory = new();
    private readonly FakeTimeProvider _time = new(_now);

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value))).Build();
    }

    private string File(string source, string name, int bytes, TimeSpan age)
    {
        var path = _directory.Combine(source, name);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, new byte[bytes]);
        System.IO.File.SetLastWriteTimeUtc(path, (_now - age).UtcDateTime);

        return path;
    }

    [Fact]
    public void TheJournalsAreKeptSevenDaysAnd250MegabytesByDefault()
    {
        var retention = JournalRetention.Read(Configuration());

        Assert.Equal(TimeSpan.FromDays(7), retention.MaxAge);
        Assert.Equal(250L * 1024 * 1024, retention.MaxBytes);
    }

    [Fact]
    public void TheRetentionIsReadFromItsSettingsAndZeroKeepsEverything()
    {
        var retention = JournalRetention.Read(Configuration((JournalRetention.DaysKey, "0"), (JournalRetention.MegabytesKey, "10")));

        Assert.Null(retention.MaxAge);
        Assert.Equal(10L * 1024 * 1024, retention.MaxBytes);
        Assert.Throws<InvalidOperationException>(() => JournalRetention.Read(Configuration((JournalRetention.DaysKey, "-1"))));
    }

    [Fact]
    public void TheFilesOlderThanTheDaysGoThenTheOldestWhileTheRestIsOverTheSize()
    {
        var old = File("3-11", "2026-11-10.txt", 10, TimeSpan.FromDays(14));
        var older = File("host", "2026-11-20.txt", 60, TimeSpan.FromDays(4));
        var recent = File("3-11", "2026-11-23.txt", 60, TimeSpan.FromDays(1));
        var today = File("host", "2026-11-24.txt", 30, TimeSpan.FromHours(1));
        var other = File("3-11", "notes.md", 500, TimeSpan.FromDays(30));
        var retention = new JournalRetention { MaxAge = TimeSpan.FromDays(7), MaxBytes = 100 };

        var deleted = retention.Sweep(_directory.Path, _time);

        Assert.Equal(2, deleted);
        Assert.False(System.IO.File.Exists(old));
        Assert.False(System.IO.File.Exists(older));
        Assert.True(System.IO.File.Exists(recent));
        Assert.True(System.IO.File.Exists(today));
        Assert.True(System.IO.File.Exists(other));
    }

    [Fact]
    public void AMissingFolderHoldsNothingToSweep()
    {
        Assert.Equal(0, JournalRetention.Default.Sweep(_directory.Combine("none"), _time));
    }
}
