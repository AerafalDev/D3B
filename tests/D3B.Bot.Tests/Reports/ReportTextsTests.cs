using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Reports;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Reports;

public sealed class ReportTextsTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 10, 12, 30, 15, 250, TimeSpan.Zero);

    private static readonly ReportIdentity _identity = new(11, "Aldebarane-du-Nord", ["Zephyrine"]);

    [Fact]
    public void TheJournalIsOneLineAnEntryWithItsTimeAndDepthAndTheCharactersNameIsItsNumber()
    {
        JournalEntry[] entries =
        [
            new WalkEntry(_at, 1, 188744706, 303, WalkOutcome.Arrived, TimeSpan.FromSeconds(2)),
            new NoteEntry(_at.AddSeconds(1), "harvest", "Aldebarane-du-Nord reached the bank; ALDEBARANE-DU-NORD is full, and Zephyrine is not."),
        ];

        var text = ReportTexts.Journal(entries, _identity);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("2026-11-10T12:30:15.250", lines[0], StringComparison.Ordinal);
        Assert.Contains("WalkEntry", lines[0], StringComparison.Ordinal);
        Assert.Contains("303", lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Aldebarane", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Zephyrine", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("character 11 reached the bank; character 11 is full, and another character of the account is not.", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntryHoldingALineBreakStaysOnOneLineSoTheTextCanBeReadAndDiffed()
    {
        var text = ReportTexts.Journal([new NoteEntry(_at, "b", "one\ntwo\r\nthree")], _identity);

        Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void TheTraceIsOneLineADecisionAndSaysTheNameOfNothing()
    {
        TraceEntry[] entries =
        [
            new TraceEntry(_at, _at.AddMinutes(-3), "Harvest Aldebarane-du-Nord's ash", "Gathering", TraceKind.Played, TraceOutcome.Played) { Subject = "Zephyrine" },
        ];

        var text = ReportTexts.Trace(entries, _identity);

        Assert.StartsWith("2026-11-10T12:30:15.250", text, StringComparison.Ordinal);
        Assert.Contains("Played", text, StringComparison.Ordinal);
        Assert.Contains("Gathering", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Aldebarane", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Zephyrine", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ANameOfOneCharacterIsLeftAloneForFearOfHidingAWordOfTheText()
    {
        var identity = new ReportIdentity(5, "A", []);

        Assert.Equal("A map and a bank", identity.Hide("A map and a bank"));
    }

    [Fact]
    public void TheSettingsThatDifferAreJsonSortedByKeyAndAnEmptyOneIsAnEmptyObject()
    {
        var text = ReportTexts.Settings(new Dictionary<string, string?> { ["Delays:Display"] = "1.5", ["Banking:FullAt"] = "90", ["Gone"] = null });

        Assert.Equal("""
            {
              "Banking:FullAt": "90",
              "Delays:Display": "1.5"
            }
            """.ReplaceLineEndings("\n"), text.ReplaceLineEndings("\n").TrimEnd());
        Assert.Equal("{}", ReportTexts.Settings(new Dictionary<string, string?>()).Trim());
    }
}
