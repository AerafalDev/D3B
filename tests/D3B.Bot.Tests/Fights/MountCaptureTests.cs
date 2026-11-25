using System.Collections.Immutable;
using D3B.Bot.Fights;
using D3B.Bot.Runtime;
using D3B.Data.Monsters;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Fights;

/// <summary>The wild mounts a character may still tame in its fight, and who tames them (D533).</summary>
public sealed class MountCaptureTests
{
    private const int Muldo = 4_434;
    private const int Dragodinde = 171;
    private const int Larva = 489;

    private static readonly MonsterCatalog _monsters = new(
        new[]
        {
            new Monster { Id = Dragodinde, Race = MountCapture.Dragodindes, FirstGrade = 0, GradeCount = 1 },
            new Monster { Id = Larva, Race = 1, FirstGrade = 1, GradeCount = 1 },
            new Monster { Id = Muldo, Race = MountCapture.Muldos, FirstGrade = 2, GradeCount = 1 },
        },
        new[] { new MonsterGrade { Grade = 1, Level = 62 }, new MonsterGrade { Grade = 1, Level = 10 }, new MonsterGrade { Grade = 1, Level = 62 } },
        Array.Empty<MonsterSpell>(),
        Array.Empty<byte>(),
        Array.Empty<MonsterRatio>(),
        Array.Empty<MonsterRace>(),
        Array.Empty<MonsterRace>());

    private static Fighter Monster(long id, int monsterId, FightTeam team = FightTeam.Defender, bool alive = true, params int[] states)
    {
        return new Fighter
        {
            Id = id,
            Team = team,
            IsAlive = alive,
            MonsterId = monsterId,
            Buffs = [.. states.Select((state, index) => new Bot.Fights.FightBuff { Uid = index + 1, ActionId = 950, Kind = FightBuffKind.State, SpellId = 32_616, Parameter = state })],
        };
    }

    private static FightState Fight(params Fighter[] fighters)
    {
        return new FightState { Team = FightTeam.Challenger, Fighters = fighters.ToImmutableSortedDictionary(fighter => fighter.Id, fighter => fighter) };
    }

    [Fact]
    public void TheEnemiesAliveOfTheRacesCapturedAndNotYetTamedAreTargets()
    {
        var fight = Fight(
            Monster(-1, Muldo),
            Monster(-2, Muldo, states: MountCapture.Tamed),
            Monster(-3, Muldo, alive: false),
            Monster(-4, Dragodinde),
            Monster(-5, Larva),
            Monster(-6, Muldo, FightTeam.Challenger));

        Assert.Equal([-1L], MountCapture.Targets(fight, _monsters, [MountCapture.Muldos]));
    }

    [Fact]
    public void NoRaceCapturedLeavesNoTarget()
    {
        Assert.Empty(MountCapture.Targets(Fight(Monster(-1, Muldo)), _monsters, []));
    }

    [Theory]
    [InlineData("Alone", 1, true)]
    [InlineData("Leads", 1, true)]
    [InlineData("Follows", 1, false)]
    [InlineData("Alone", -7, false)]
    public void OnlyTheMainTamesOnItsOwnTurns(string role, long playing, bool tames)
    {
        // The owner's choice (D533): a follower keeps its nets, and a summon the character controls has none.
        Assert.Equal(tames, FightPlay.Tames(Enum.Parse<FightRole>(role), playing, 1));
    }
}
