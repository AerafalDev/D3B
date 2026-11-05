using D3B.Bot.Npcs;

namespace D3B.Bot.Tests.Npcs;

public sealed class KnownNpcsTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ThePlacesAnNpcWasLastSeenAtKeepTheEarlierOnesWhenTheServerGaveNone()
    {
        var known = new KnownNpcs();

        known.Saw(1, -5, 10, 300, 2, _start);
        known.Saw(1, -6, 10, null, null, _start.AddMinutes(1));

        var npc = Assert.Single(known.OnMap(1)).Npc;

        Assert.Equal((-6, 300, 2, 2), (npc.ActorId, npc.Cell, npc.Direction, npc.Seen));
        Assert.Equal((_start, _start.AddMinutes(1)), (npc.FirstSeen, npc.LastSeen));
    }

    [Fact]
    public void TheSameQuestionWithOtherRepliesIsAnotherQuestion()
    {
        var known = new KnownNpcs();

        known.Saw(1, -5, 10, 300, 2, _start);
        known.Asked(1, 10, 25_061, [], opened: true, _start);
        known.Asked(1, 10, 25_061, [31_543], opened: true, _start);
        known.Asked(1, 10, 25_061, [31_543], opened: false, _start.AddMinutes(1));

        var questions = Assert.Single(known.OnMap(1)).Questions;

        Assert.Equal([("", 1, 1), ("31543", 1, 2)], questions.OrderBy(question => KnownNpcs.Key(question.Replies), StringComparer.Ordinal).Select(question => (KnownNpcs.Key(question.Replies), question.Opened, question.Seen)));
        Assert.Equal(2, known.QuestionCount);
    }

    [Fact]
    public void AReplyThatLeadsToTwoThingsKeepsBothCounted()
    {
        var known = new KnownNpcs();

        known.Saw(1, -5, 10, 300, 2, _start);
        known.Answered(1, 10, 30_638, 36_977, NpcOutcomeKind.MapChange, 192_416_776, _start);
        known.Answered(1, 10, 30_638, 36_977, NpcOutcomeKind.MapChange, 192_416_776, _start.AddMinutes(1));
        known.Answered(1, 10, 30_638, 36_977, NpcOutcomeKind.End, 0, _start);

        Assert.Equal([(NpcOutcomeKind.MapChange, 2), (NpcOutcomeKind.End, 1)], Assert.Single(known.OnMap(1)).Outcomes.OrderByDescending(outcome => outcome.Count).Select(outcome => (outcome.Kind, outcome.Count)));
    }

    [Fact]
    public void WhatWasTakenBackIsKnownWithoutBeingLearnedAgain()
    {
        var known = new KnownNpcs();
        var told = 0;

        known.NpcSeen += _ => told++;
        known.Remember(new KnownMapNpc(1, 10, -5, 300, 2, 4, _start, _start));

        Assert.Equal(0, told);
        Assert.Equal(1, known.NpcCount);

        known.Saw(1, -5, 10, 300, 2, _start.AddDays(1));

        Assert.Equal(1, told);
        Assert.Equal(5, Assert.Single(known.OnMap(1)).Npc.Seen);
    }
}
