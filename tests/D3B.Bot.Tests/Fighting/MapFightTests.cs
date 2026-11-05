using D3B.Bot.Characters;
using D3B.Bot.Fighting;

namespace D3B.Bot.Tests.Fighting;

public sealed class MapFightTests
{
    private static MapMonsterGroup Group(int? cell, params int[] monsters)
    {
        return new MapMonsterGroup(-1, cell, [.. monsters.Select(monster => new MonsterInGroup(monster, 1, 10))]);
    }

    [Fact]
    public void WithNoMonsterChosenEveryGroupOfTheSizeAskedForIsLetThrough()
    {
        Assert.True(MapFight.Allows(Group(30, 1, 2), [], 1, 8));
        Assert.True(MapFight.Allows(Group(30, 1, 2), [], 2, 2));
        Assert.False(MapFight.Allows(Group(30, 1, 2), [], 3, 8));
        Assert.False(MapFight.Allows(Group(30, 1, 2), [], 1, 1));
    }

    [Fact]
    public void AGroupMustHoldOneOfTheMonstersChosen()
    {
        Assert.True(MapFight.Allows(Group(30, 1, 2), [2, 9], 1, 8));
        Assert.False(MapFight.Allows(Group(30, 1, 2), [9], 1, 8));
    }

    [Fact]
    public void AGroupWithNoCellIsNeverLetThroughThereBeingNowhereToWalkTo()
    {
        Assert.False(MapFight.Allows(Group(null, 1), [], 1, 8));
    }
}
