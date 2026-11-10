using System.Text.Json;
using D3B.Bot.Npcs;
using D3B.Bot.Reports;
using D3B.Storage.Accounts;
using D3B.Storage.Fights;
using D3B.Storage.Npcs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests;

public sealed class ReportKnowledgeTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WhatTheBotsLearnedIsAFileOfIdsWithTheNamesOfNoAccountNorCharacter()
    {
        await using var storage = await new TestStorage().StartedAsync();
        var contexts = storage.Services.GetRequiredService<IDbContextFactory<StorageContext>>();

        await using (var context = await contexts.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            context.Placements.Add(new LearnedPlacement { FromMapId = 188744706, FromAreaId = 5, FromSubAreaId = 95, PosX = 4, PosY = -2, MapId = 191104002, RedCells = "112,126", BlueCells = "300,301", Seen = 3, FirstSeenAt = _start, LastSeenAt = _start.AddDays(1) });
            context.MapNpcs.Add(new LearnedMapNpc { MapId = 153880835, NpcId = 4398, ActorId = -7, Cell = 300, Direction = 3, Seen = 2, FirstSeenAt = _start, LastSeenAt = _start });
            context.NpcQuestions.Add(new LearnedNpcQuestion { MapId = 153880835, NpcId = 4398, MessageId = 30637, Replies = "36979", Opened = 1, Seen = 2, FirstSeenAt = _start, LastSeenAt = _start });
            context.NpcOutcomes.Add(new LearnedNpcOutcome { MapId = 153880835, NpcId = 4398, MessageId = 30638, ReplyId = 36977, Kind = NpcOutcomeKind.MapChange, Target = 192416776, Count = 2, LastSeenAt = _start });
            context.Accounts.Add(new KnownAccount { Id = 99, Nickname = "secret-login", Tag = "1234", FirstSeen = _start, LastSeen = _start });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var part = await ReportKnowledge.ReadAsync(contexts, TestContext.Current.CancellationToken);
        var text = System.Text.Encoding.UTF8.GetString(part.Data);
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;

        Assert.Equal(ReportFiles.Knowledge, part.Name);
        Assert.DoesNotContain("secret-login", text, StringComparison.Ordinal);
        Assert.Equal(["fightPlacements", "mapNpcs", "npcOutcomes", "npcQuestions"], root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

        var placement = root.GetProperty("fightPlacements").EnumerateArray().Single();

        Assert.Equal(191104002, placement.GetProperty("mapId").GetInt64());
        Assert.Equal("112,126", placement.GetProperty("redCells").GetString());
        Assert.Equal(3, placement.GetProperty("seen").GetInt32());
        Assert.Equal(4398, root.GetProperty("mapNpcs").EnumerateArray().Single().GetProperty("npcId").GetInt32());
        Assert.Equal("36979", root.GetProperty("npcQuestions").EnumerateArray().Single().GetProperty("replies").GetString());
        Assert.Equal("mapChange", root.GetProperty("npcOutcomes").EnumerateArray().Single().GetProperty("kind").GetString());
    }

    [Fact]
    public async Task AnEmptyDatabaseGivesFourEmptyLists()
    {
        await using var storage = await new TestStorage().StartedAsync();
        var part = await ReportKnowledge.ReadAsync(storage.Services.GetRequiredService<IDbContextFactory<StorageContext>>(), TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(part.Data);

        Assert.All(json.RootElement.EnumerateObject(), property => Assert.Equal(0, property.Value.GetArrayLength()));
    }
}
