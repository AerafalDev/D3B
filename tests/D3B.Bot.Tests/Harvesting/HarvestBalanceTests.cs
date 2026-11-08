using System.Collections.Frozen;
using D3B.Bot.Harvesting;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestBalanceTests
{
    private const int Lumberjack = 2;
    private const int Miner = 24;
    private const int Alchemist = 26;

    private static HarvestBalance Balance()
    {
        return new HarvestBalance([Lumberjack, Miner, Alchemist]);
    }

    [Fact]
    public void BeforeAnyHarvestTheProfessionsComeInTheOrderOfTheSettings()
    {
        Assert.Equal([Lumberjack, Miner, Alchemist], Balance().Order());
    }

    [Fact]
    public void TheProfessionHarvestedLeastLatelyComesFirstAndTheSettingsBreakATie()
    {
        var balance = Balance();

        balance.Record(Lumberjack);
        balance.Record(Lumberjack);
        balance.Record(Miner);

        Assert.Equal([Alchemist, Miner, Lumberjack], balance.Order());

        balance.Record(Alchemist);
        balance.Record(Alchemist);

        Assert.Equal([Miner, Lumberjack, Alchemist], balance.Order());
    }

    [Fact]
    public void OnlyTheLastHarvestsCountSoAProfessionLongAbsentIsNotOwedEverything()
    {
        var balance = Balance();

        for (var harvest = 0; harvest < HarvestBalance.Window; harvest++)
            balance.Record(Lumberjack);

        Assert.Equal([Miner, Alchemist, Lumberjack], balance.Order());

        for (var harvest = 0; harvest < HarvestBalance.Window; harvest++)
            balance.Record(Miner);

        // The lumberjack's harvests left the window: it is on a par with the alchemist, and ahead of the miner.
        Assert.Equal([Lumberjack, Alchemist, Miner], balance.Order());
    }

    [Fact]
    public void AProfessionNotInTheSettingsStillCountsOnceHarvested()
    {
        var balance = Balance();

        balance.Record(36);

        Assert.Equal([Lumberjack, Miner, Alchemist, 36], balance.Order());
    }

    [Fact]
    public void ThePrioritiesPutTheProfessionsInTurnAndThenTheHighestLevelFirst()
    {
        var targets = new[]
        {
            new HarvestTarget(1, 100, Lumberjack, 1, -1),
            new HarvestTarget(2, 101, Lumberjack, 60, -60),
            new HarvestTarget(3, 102, Miner, 1, -1),
            new HarvestTarget(4, 103, Alchemist, 30, -30),
        }.ToFrozenDictionary(target => target.SkillId);
        var balance = Balance();

        balance.Record(Lumberjack);
        balance.Record(Lumberjack);
        balance.Record(Miner);

        var priorities = balance.Prioritise(targets);
        var order = priorities.Values.OrderBy(target => target.Priority).Select(target => target.SkillId);

        // Alchemist, the least harvested, then the miner, then the lumberjack's level 60 before its level 1.
        Assert.Equal([4, 3, 2, 1], order);
        Assert.Same(priorities, balance.Prioritise(targets));

        balance.Record(Alchemist);
        balance.Record(Alchemist);
        balance.Record(Alchemist);

        Assert.Equal([3, 2, 1, 4], balance.Prioritise(targets).Values.OrderBy(target => target.Priority).Select(target => target.SkillId));
    }
}
