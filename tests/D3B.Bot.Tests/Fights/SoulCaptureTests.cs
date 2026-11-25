using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Data.Monsters;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Fights;

/// <summary>The archmonsters whose souls a character captures, and the soul stone it wears for them (D565).</summary>
public sealed class SoulCaptureTests
{
    private const int Larva = 489;
    private const int Archmonster = 2_500;

    private static readonly MonsterCatalog _monsters = new(
        new[]
        {
            new Monster { Id = Larva, Race = 1, FirstGrade = 0, GradeCount = 1 },
            new Monster { Id = Archmonster, Race = 1, Properties = MonsterProperties.IsMiniBoss, FirstGrade = 1, GradeCount = 1 },
        },
        new[] { new MonsterGrade { Grade = 1, Level = 10 }, new MonsterGrade { Grade = 1, Level = 12 } },
        Array.Empty<MonsterSpell>(),
        Array.Empty<byte>(),
        Array.Empty<MonsterRatio>(),
        Array.Empty<MonsterRace>(),
        Array.Empty<MonsterRace>());

    private static InventoryItem Stack(int uid, int itemId, int position = InventoryItem.NotWorn)
    {
        return new InventoryItem(uid, itemId, 2, position);
    }

    private static Fighter Monster(long id, int monsterId, FightTeam team = FightTeam.Defender, bool alive = true)
    {
        return new Fighter { Id = id, Team = team, IsAlive = alive, MonsterId = monsterId };
    }

    private static FightState Fight(params Fighter[] fighters)
    {
        return new FightState { Team = FightTeam.Challenger, Fighters = fighters.ToImmutableSortedDictionary(fighter => fighter.Id, fighter => fighter) };
    }

    [Theory]
    [InlineData(12, 9_686)]
    [InlineData(50, 9_686)]
    [InlineData(51, 9_688)]
    [InlineData(151, 9_689)]
    [InlineData(191, 9_690)]
    public void TheStoneWornIsTheSmallestCarriedThatCapturesTheGroupsHighestLevel(int level, int stone)
    {
        // Measured in the client 3.7.14.18 (D533): the five crafted stones capture up to level 50, 100, 150, 190, and every
        // level; the stone worn must capture the group's highest level monster.
        var carried = new[] { Stack(1, 9_690), Stack(2, 9_689), Stack(3, 9_688), Stack(4, 9_686), Stack(5, 32_521) };

        Assert.Equal(stone, SoulCapture.StoneFor(carried, level)?.ItemId);
    }

    [Fact]
    public void AStoneWornThatSufficesStaysAndNoneBigEnoughLeavesNone()
    {
        var worn = Stack(1, 9_687, InventoryItem.FightConsumable);

        Assert.Same(worn, SoulCapture.StoneFor([Stack(2, 9_687), worn], 80));
        Assert.Null(SoulCapture.StoneFor([Stack(3, 9_686), Stack(4, 32_521)], 51));
    }

    [Fact]
    public void AGroupHoldingAnArchmonsterIsOneToCaptureAtItsHighestLevel()
    {
        var group = new MapMonsterGroup(-1, 100, [new MonsterInGroup(Larva, 1, 140), new MonsterInGroup(Archmonster, 1, 120)]);
        var plain = new MapMonsterGroup(-2, 100, [new MonsterInGroup(Larva, 1, 140)]);

        Assert.Equal(140, SoulCapture.LevelToCapture(group, _monsters));
        Assert.Null(SoulCapture.LevelToCapture(plain, _monsters));
    }

    [Fact]
    public void AFightHoldsASoulToCaptureWhileAnArchmonsterIsAmongTheEnemiesDeadOrAlive()
    {
        // The spell captures the souls of the creatures vanquished at the fight's end (D533): one already dead still counts.
        Assert.True(SoulCapture.HoldsArchmonster(Fight(Monster(-1, Larva), Monster(-2, Archmonster, alive: false)), _monsters));
        Assert.False(SoulCapture.HoldsArchmonster(Fight(Monster(-1, Larva), Monster(-2, Archmonster, FightTeam.Challenger)), _monsters));
    }
}
