using System.Collections.Frozen;
using D3B.Bot.Harvesting;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Harvesting;

public sealed class MapHarvestTests
{
    private static FrozenDictionary<int, HarvestTarget> Targets(int level = 1)
    {
        return new Dictionary<int, HarvestTarget> { [TestPlay.TreeSkillId] = new(TestPlay.TreeSkillId, TestPlay.Ash, TestPlay.Lumberjack, level, 0) }.ToFrozenDictionary();
    }

    private static Task<int> Harvest(BotActor bot, FrozenDictionary<int, HarvestTarget> targets)
    {
        return bot.RunAsync((context, token) => MapHarvest.HarvestAsync(context, targets, token), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EveryElementTheTargetsNameIsHarvestedUntilNoneIsLeftThenTheActionIsOver()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var harvest = Harvest(bot, Targets());

        await TestWalks.WalkedAsync(play, connection, 1);

        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId }, await connection.SentAsync(2));

        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) });
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 15 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent { Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 } });

        Assert.Equal(1, await TestWalks.AdvanceUntilDoneAsync(play.Time, harvest));
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task AMapWithNothingTheTargetsNameSendsNothingAndHarvestsNone()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.Equal(0, await Harvest(bot, FrozenDictionary<int, HarvestTarget>.Empty));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AResourceAboveTheProfessionLevelIsLeftAlone()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.Equal(0, await Harvest(bot, Targets(level: 200)));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnElementTheServerRefusesIsLeftForTheVisitAndTheActionGoesOn()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var harvest = Harvest(bot, Targets());

        await TestWalks.WalkedAsync(play, connection, 1);
        Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(2));
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeInstanceId });

        Assert.Equal(0, await TestWalks.AdvanceUntilDoneAsync(play.Time, harvest));
    }
}
