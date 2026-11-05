using D3B.Bot.Npcs;
using D3B.Data.Tests;
using D3B.Storage.Npcs;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests.Npcs;

public sealed class NpcRecorderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task TheNpcsAndTheirDialogsAreKnownFromTheNextRunsStart()
    {
        var path = _directory.Combine("d3b.db");
        var start = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

        await using (var first = await new TestStorage(path).StartedAsync())
        {
            var known = first.Services.GetRequiredService<KnownNpcs>();

            known.Saw(153_880_835, -7, 4_398, 300, 3, start);
            known.Saw(153_880_835, -7, 4_398, 301, 4, start.AddMinutes(3));
            known.Asked(153_880_835, 4_398, 30_637, [36_979], opened: true, start);
            known.Asked(153_880_835, 4_398, 30_637, [], opened: true, start);
            known.Answered(153_880_835, 4_398, 30_638, 36_977, NpcOutcomeKind.MapChange, 192_416_776, start);
            known.Answered(153_880_835, 4_398, 30_638, 36_977, NpcOutcomeKind.End, 0, start);
            await first.Services.GetRequiredService<NpcRecorder>().WrittenAsync();
        }

        await using (var second = await new TestStorage(path).StartedAsync())
        {
            var known = second.Services.GetRequiredService<KnownNpcs>();

            Assert.Equal((1, 2, 2), (known.NpcCount, known.QuestionCount, known.OutcomeCount));
            Assert.Contains(second.Log.GetSnapshot(), record => record.Message.Contains(", 1 NPCs, 2 NPC questions, 2 NPC outcomes,", StringComparison.Ordinal));

            // Counted on from what the database kept.
            known.Saw(153_880_835, -9, 4_398, 302, 5, start.AddDays(1));
            known.Answered(153_880_835, 4_398, 30_638, 36_977, NpcOutcomeKind.MapChange, 192_416_776, start.AddDays(1));
            await second.Services.GetRequiredService<NpcRecorder>().WrittenAsync();
        }

        await using var next = await new TestStorage(path).StartedAsync();
        var npc = Assert.Single(next.Services.GetRequiredService<KnownNpcs>().OnMap(153_880_835));

        Assert.Equal((4_398, -9, 302, 5, 3), (npc.Npc.NpcId, npc.Npc.ActorId, npc.Npc.Cell, npc.Npc.Direction, npc.Npc.Seen));
        Assert.Equal((start, start.AddDays(1)), (npc.Npc.FirstSeen, npc.Npc.LastSeen));
        Assert.Equal([(0, 1), (36_979, 1)], npc.Questions.Select(question => (question.Replies.IsEmpty ? 0 : question.Replies[0], question.Seen)).Order());
        Assert.Equal((2, 1), (npc.Outcomes.Single(outcome => outcome.Kind is NpcOutcomeKind.MapChange).Count, npc.Outcomes.Single(outcome => outcome.Kind is NpcOutcomeKind.End).Count));
        Assert.Equal(192_416_776, npc.Outcomes.Single(outcome => outcome.Kind is NpcOutcomeKind.MapChange).Target);
    }
}
