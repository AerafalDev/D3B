using System.Collections.Immutable;
using D3B.Bot.Fights;
using D3B.Engine.Tactics;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Fights;

/// <summary>The weights the AI plays a fight with: prudent when it would lose, a little bolder beside allies of its group (D454, D460).</summary>
public sealed class FightWeightsTests
{
    private const long Self = 1;

    private static FightState Fight(params Fighter[] others)
    {
        var fighters = others.Append(new Fighter { Id = Self, Team = FightTeam.Challenger, BreedId = 8 });

        return new FightState { Team = FightTeam.Challenger, Fighters = fighters.ToImmutableSortedDictionary(fighter => fighter.Id, fighter => fighter) };
    }

    [Fact]
    public void BesideACharacterOfItsTeamTheAIPlaysALittleBolder()
    {
        var fight = Fight(new Fighter { Id = 2, Team = FightTeam.Challenger, BreedId = 11 }, new Fighter { Id = -1, Team = FightTeam.Defender, MonsterId = 31 });

        Assert.Same(TacticWeights.Group, FightPlay.WeightsOf(fight, Self, risky: false));
        Assert.True(TacticWeights.Group.Threat < TacticWeights.Default.Threat);
        // The next turn weighed as alone (D460): weighed more, it left whole the monsters a character could kill now.
        Assert.Equal(TacticWeights.Default.Approach, TacticWeights.Group.Approach);
    }

    [Fact]
    public void AloneWithItsSummonsOrBesideADeadAllyTheAIPlaysAsBefore()
    {
        var summoned = Fight(new Fighter { Id = -5, Team = FightTeam.Challenger, MonsterId = 3, SummonerId = Self });
        var dead = Fight(new Fighter { Id = 2, Team = FightTeam.Challenger, BreedId = 11, IsAlive = false });
        var enemy = Fight(new Fighter { Id = 3, Team = FightTeam.Defender, BreedId = 11 });

        Assert.Same(TacticWeights.Default, FightPlay.WeightsOf(summoned, Self, risky: false));
        Assert.Same(TacticWeights.Default, FightPlay.WeightsOf(dead, Self, risky: false));
        Assert.Same(TacticWeights.Default, FightPlay.WeightsOf(enemy, Self, risky: false));
    }

    [Fact]
    public void AMuleOfTheTeamIsNoFightingAlly()
    {
        // The owner's choice (D579): a mule passes its turns, so the AI beside it plays as alone.
        var fight = Fight(new Fighter { Id = 2, Team = FightTeam.Challenger, BreedId = 11 }, new Fighter { Id = 3, Team = FightTeam.Challenger, BreedId = 11 });

        Assert.Same(TacticWeights.Default, FightPlay.WeightsOf(fight, Self, risky: false, ally => ally is 2 or 3));
        Assert.Same(TacticWeights.Group, FightPlay.WeightsOf(fight, Self, risky: false, ally => ally is 2));
    }

    [Fact]
    public void AFightTooRiskyIsPlayedPrudentlyInAGroupToo()
    {
        var fight = Fight(new Fighter { Id = 2, Team = FightTeam.Challenger, BreedId = 11 });

        Assert.Same(TacticWeights.Prudent, FightPlay.WeightsOf(fight, Self, risky: true));
    }
}
