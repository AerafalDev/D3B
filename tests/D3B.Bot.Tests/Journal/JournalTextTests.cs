using System.Globalization;
using D3B.Bot.Hosting;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Tests.Journal;

public sealed class JournalTextTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 24, 21, 5, 3, 120, TimeSpan.FromHours(1));

    [Fact]
    public void AnEntryIsOneLineFromItsUtcTimeWithEveryFieldAndItsDepth()
    {
        var line = JournalText.Of(new WalkEntry(_at, 1, 188744706, 303, WalkOutcome.Arrived, TimeSpan.FromSeconds(2.5)));

        Assert.Equal(_at, line.At);
        Assert.StartsWith("2026-11-24T20:05:03.120Z   WalkEntry { ", line.Text, StringComparison.Ordinal);
        Assert.Contains("Cell = 303", line.Text, StringComparison.Ordinal);
        Assert.Contains("Outcome = Arrived", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntryIsWrittenInNoLanguageWhateverTheThreadsCulture()
    {
        var culture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            var line = JournalText.Of(new WalkEntry(_at, 0, 1, 2, WalkOutcome.Arrived, TimeSpan.FromSeconds(2.5)));

            Assert.Contains("11/24/2026", line.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("24/11/2026", line.Text, StringComparison.Ordinal);
            Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void ALineBreakInAnEntryStaysOnItsLine()
    {
        var line = JournalText.Of(new NoteEntry(_at, "harvest", "one\ntwo\r\nthree"));

        Assert.DoesNotContain('\n', line.Text);
        Assert.DoesNotContain('\r', line.Text);
    }

    [Fact]
    public void AProblemOfTheHostSaysItsLevelItsLogAndItsExceptionOnOneLine()
    {
        var line = JournalText.Of(new HostProblem(_at, LogLevel.Error, "D3B.Network.Relay", "The relay failed", "System.IO.IOException: The pipe broke.\r\n   at Relay.Read()"));

        Assert.Equal("2026-11-24T20:05:03.120Z Error D3B.Network.Relay: The relay failed | System.IO.IOException: The pipe broke.    at Relay.Read()", line.Text);
    }
}
