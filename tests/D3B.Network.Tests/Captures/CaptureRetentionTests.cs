using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Relay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Network.Tests.Captures;

public sealed class CaptureRetentionTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(_now);

    private static string Capture(TemporaryDirectory directory, string name, TimeSpan age, int length = 1000)
    {
        var path = Path.Combine(directory.Path, name + CaptureWriter.FileExtension);

        File.WriteAllBytes(path, new byte[length]);
        File.SetLastWriteTimeUtc(path, (_now - age).UtcDateTime);

        return path;
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();
    }

    private static CaptureRetention Read(params (string Key, string Value)[] values)
    {
        return CaptureRetention.Read(Configuration(values), RelaySettings.CapturesRetentionDaysKey, RelaySettings.CapturesRetentionMegabytesKey);
    }

    [Fact]
    public void ByDefaultCapturesAreKeptSevenDaysAndAGigabyte()
    {
        Assert.Equal(TimeSpan.FromDays(7), CaptureRetention.Default.MaxAge);
        Assert.Equal(1024L * 1024 * 1024, CaptureRetention.Default.MaxBytes);
        Assert.Equal(CaptureRetention.Default, Read());
    }

    [Fact]
    public void ACaptureOlderThanTheLimitIsDeletedAndAYoungerOneKept()
    {
        using var directory = new TemporaryDirectory();
        var old = Capture(directory, "old", TimeSpan.FromDays(8));
        var young = Capture(directory, "young", TimeSpan.FromDays(6));

        var sweep = CaptureRetention.Default.Sweep(directory.Path, _time);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(young));
        Assert.Equal(new CaptureSweep(1, 1000, 1, 1000), sweep);
    }

    [Fact]
    public void WhenTheSetIsOverTheSizeTheOldestAreDeletedFirstUntilItFits()
    {
        using var directory = new TemporaryDirectory();
        var oldest = Capture(directory, "a", TimeSpan.FromHours(30));
        var middle = Capture(directory, "b", TimeSpan.FromHours(20));
        var newest = Capture(directory, "c", TimeSpan.FromHours(10));

        var sweep = new CaptureRetention { MaxAge = TimeSpan.FromDays(7), MaxBytes = 2500 }.Sweep(directory.Path, _time);

        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(middle));
        Assert.True(File.Exists(newest));
        Assert.Equal(new CaptureSweep(1, 1000, 2, 2000), sweep);
    }

    [Fact]
    public void ACaptureStillOpenIsNeverDeletedWhateverItsAgeOrTheSizeOfTheSet()
    {
        using var directory = new TemporaryDirectory();
        var header = new CaptureHeader { StartedAt = _now.AddDays(-30), ClientVersion = "3.7.13.17", Listener = "127.0.13.59:5556", ServerHost = "game.test", Server = "192.0.2.1:5555" };
        var (writer, path) = CaptureWriter.Create(directory.Path, "open", header, _time);
        var closed = Capture(directory, "closed", TimeSpan.FromDays(9));

        File.SetLastWriteTimeUtc(path, (_now - TimeSpan.FromDays(30)).UtcDateTime);

        CaptureSweep sweep;

        try
        {
            sweep = new CaptureRetention { MaxAge = TimeSpan.FromDays(7), MaxBytes = 1 }.Sweep(directory.Path, _time);
        }
        finally
        {
            writer.Dispose();
        }

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(closed));
        Assert.Equal(1, sweep.Deleted);
        Assert.Equal(1, sweep.Kept);
    }

    [Fact]
    public void ALimitLeftOutIsNoLimit()
    {
        using var directory = new TemporaryDirectory();
        var ancient = Capture(directory, "ancient", TimeSpan.FromDays(400), length: 5000);

        var sweep = new CaptureRetention().Sweep(directory.Path, _time);

        Assert.True(File.Exists(ancient));
        Assert.Equal(new CaptureSweep(0, 0, 1, 5000), sweep);
    }

    [Fact]
    public void OnlyCaptureFilesAreTouched()
    {
        using var directory = new TemporaryDirectory();
        var other = Path.Combine(directory.Path, "notes.txt");
        var nested = Directory.CreateDirectory(Path.Combine(directory.Path, "nested"));

        File.WriteAllText(other, "a player's file");
        File.SetLastWriteTimeUtc(other, (_now - TimeSpan.FromDays(100)).UtcDateTime);
        File.WriteAllBytes(Path.Combine(nested.FullName, "old" + CaptureWriter.FileExtension), new byte[10]);
        File.SetLastWriteTimeUtc(Path.Combine(nested.FullName, "old" + CaptureWriter.FileExtension), (_now - TimeSpan.FromDays(100)).UtcDateTime);

        var sweep = CaptureRetention.Default.Sweep(directory.Path, _time);

        Assert.True(File.Exists(other));
        Assert.True(File.Exists(Path.Combine(nested.FullName, "old" + CaptureWriter.FileExtension)));
        Assert.Equal(default, sweep);
    }

    [Fact]
    public void AMissingDirectoryHoldsNothingToSweep()
    {
        using var directory = new TemporaryDirectory();

        var sweep = CaptureRetention.Default.Sweep(Path.Combine(directory.Path, "never-made"), _time);

        Assert.Equal(default, sweep);
    }

    [Fact]
    public void TheConfigurationSetsTheLimitsAndZeroLiftsThem()
    {
        var set = Read((RelaySettings.CapturesRetentionDaysKey, "3"), (RelaySettings.CapturesRetentionMegabytesKey, "200"));
        var lifted = Read((RelaySettings.CapturesRetentionDaysKey, "0"), (RelaySettings.CapturesRetentionMegabytesKey, "0"));

        Assert.Equal(TimeSpan.FromDays(3), set.MaxAge);
        Assert.Equal(200L * 1024 * 1024, set.MaxBytes);
        Assert.Null(lifted.MaxAge);
        Assert.Null(lifted.MaxBytes);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("a week")]
    [InlineData("99999999999")]
    [InlineData("9000000000000")]
    public void ALimitThatIsNotAWholeNumberIsRefused(string value)
    {
        Assert.Throws<InvalidOperationException>(() => Read((RelaySettings.CapturesRetentionDaysKey, value)));
        Assert.Throws<InvalidOperationException>(() => Read((RelaySettings.CapturesRetentionMegabytesKey, value)));
    }
}
