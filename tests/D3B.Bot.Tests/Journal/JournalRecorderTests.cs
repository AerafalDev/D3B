using D3B.Bot.Hosting;
using D3B.Bot.Journal;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Journal;

public sealed class JournalRecorderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Polling.Deadline;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The recorder never wrote it.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    private JournalRecorder Recorder(TestPlay play, HostProblems problems, JournalRetention? retention = null)
    {
        return new JournalRecorder(new JournalFiles(_directory.Path), retention ?? JournalRetention.Default, play.Bots, problems, play.Time, NullLogger<JournalRecorder>.Instance);
    }

    private static HostProblem Problem(TestPlay play, string message)
    {
        return new HostProblem(play.Time.GetUtcNow(), LogLevel.Warning, "D3B.Network.Relay", message, null);
    }

    [Fact]
    public async Task AFlushWritesWhatEachBotAndTheHostSaidSinceTheLastOneAndNothingTwice()
    {
        await using var play = new TestPlay();
        var problems = new HostProblems(play.Time);
        using var recorder = Recorder(play, problems);
        var (_, bot) = await play.EnterAsync(433);
        var source = JournalFiles.SourceOf(TestPlay.Key);

        problems.Problems.Add(Problem(play, "The relay failed"));
        recorder.Flush();
        bot.Journal.Add(new NoteEntry(play.Time.GetUtcNow(), "harvest", "reached the bank"));
        recorder.Flush();
        recorder.Flush();

        var journal = recorder.Files.Read(source).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var host = recorder.Files.Read(JournalFiles.HostSource).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, journal.Length);
        Assert.Contains("BotEventEntry", journal[0], StringComparison.Ordinal);
        Assert.Contains("reached the bank", journal[1], StringComparison.Ordinal);
        Assert.Equal([$"{JournalText.TimeOf(play.Time.GetUtcNow())} Warning D3B.Network.Relay: The relay failed"], host);
    }

    [Fact]
    public async Task TheJournalOnDiskHoldsNothingOfTheTicketTheConnectionPresented()
    {
        await using var play = new TestPlay();
        using var recorder = Recorder(play, new HostProblems(play.Time));
        var (connection, bot) = await play.EnterAsync(433);

        connection.Close();
        await bot.ProcessedAsync();
        recorder.Flush();

        foreach (var file in Directory.EnumerateFiles(_directory.Path, "*", SearchOption.AllDirectories))
            Assert.DoesNotContain("ticket-of-", File.ReadAllText(file), StringComparison.Ordinal);

        Assert.NotEmpty(recorder.Files.Read(JournalFiles.SourceOf(TestPlay.Key)));
    }

    [Fact]
    public async Task TheRecorderWritesEverySecondAndOnceMoreAsTheHostStops()
    {
        await using var play = new TestPlay();
        var problems = new HostProblems(play.Time);
        using var recorder = Recorder(play, problems);

        await recorder.StartAsync(TestContext.Current.CancellationToken);
        problems.Problems.Add(Problem(play, "first"));

        await UntilAsync(() =>
        {
            play.Time.Advance(JournalRecorder.Interval);

            return recorder.Files.Read(JournalFiles.HostSource).Contains("first", StringComparison.Ordinal);
        });

        problems.Problems.Add(Problem(play, "last"));
        await recorder.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains("last", recorder.Files.Read(JournalFiles.HostSource), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhatComesAfterTheRecorderStoppedIsWrittenAsItIsDisposed()
    {
        await using var play = new TestPlay();
        var problems = new HostProblems(play.Time);
        var recorder = Recorder(play, problems);

        await recorder.StartAsync(TestContext.Current.CancellationToken);
        await recorder.StopAsync(TestContext.Current.CancellationToken);

        // The relay stops after the recorder, and the bots journal the connections it closes.
        problems.Problems.Add(Problem(play, "the relay closed"));
        recorder.Dispose();

        Assert.Contains("the relay closed", recorder.Files.Read(JournalFiles.HostSource), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRecorderSweepsTheOldJournalsAsItStarts()
    {
        await using var play = new TestPlay();
        var old = _directory.Combine("3-11", "2026-01-01.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(old)!);
        File.WriteAllText(old, "old\n");
        File.SetLastWriteTimeUtc(old, (play.Time.GetUtcNow() - TimeSpan.FromDays(30)).UtcDateTime);

        using var recorder = Recorder(play, new HostProblems(play.Time));

        await recorder.StartAsync(TestContext.Current.CancellationToken);
        await UntilAsync(() => !File.Exists(old));
        await recorder.StopAsync(TestContext.Current.CancellationToken);
    }
}
