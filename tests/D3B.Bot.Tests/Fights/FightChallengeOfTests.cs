using System.Collections.Immutable;
using D3B.Bot.Fights;
using D3B.Data.Challenges;
using D3B.Engine.Tactics;

namespace D3B.Bot.Tests.Fights;

/// <summary>The challenge a turn plays for: the fight's first easy one not yet decided nor left, never in a risky fight (D464).</summary>
public sealed class FightChallengeOfTests
{
    private const int Zombie = 1;
    private const int First = 3;
    private const int Prudent = 40;

    private static readonly int[] _none = [];
    private static readonly string[] _completions = ["TM=1,0", "CK~1", "TD>1,hc0,e1"];
    private static readonly string[] _activations = ["GL>4,0", "GL>4,0", "GL>4,0"];
    private static readonly HashSet<int> _noneLeft = [];

    private static readonly ChallengeCatalog _catalog = new(
        new Challenge[] { new(Zombie, 742, 1, 1, 1, 0, 0, 0), new(First, 851_058, 3, 1, 3, 0, 0, 0), new(Prudent, 851_067, 40, 1, 40, 0, 0, 0) },
        _none,
        _completions,
        _activations);

    private static FightState Fight(params FightChallenge[] challenges)
    {
        return new FightState { Challenges = [.. challenges] };
    }

    private static FightChallenge Challenge(int id, bool? succeeded = null)
    {
        return new FightChallenge(id, 100, ImmutableArray<FightChallengeTarget>.Empty, succeeded);
    }

    [Fact]
    public void AnEasyChallengeIsPlayedForWithWhatItsCriterionAsks()
    {
        var played = FightPlay.ChallengeOf(Fight(Challenge(Zombie)), _catalog, ChallengePlay.Easy, risky: false, _noneLeft);

        Assert.NotNull(played);
        Assert.Equal(Zombie, played.Value.Id);
        Assert.Equal(TurnMovement.OnePoint, played.Value.Challenge.Movement);
        Assert.Equal(0.25, played.Value.Challenge.MostLoss, 6);
    }

    [Fact]
    public void AChallengeThatIsNotEasyIsNotPlayedFor()
    {
        Assert.Null(FightPlay.ChallengeOf(Fight(Challenge(First)), _catalog, ChallengePlay.Easy, risky: false, _noneLeft));
        Assert.Equal(Prudent, FightPlay.ChallengeOf(Fight(Challenge(First), Challenge(Prudent)), _catalog, ChallengePlay.Easy, risky: false, _noneLeft)?.Id);
    }

    [Fact]
    public void NoneIsPlayedForInARiskyFightOrWithAProfileThatPlaysForNone()
    {
        // The fight's safety first: a challenge never loses a fight.
        Assert.Null(FightPlay.ChallengeOf(Fight(Challenge(Zombie)), _catalog, ChallengePlay.Easy, risky: true, _noneLeft));
        Assert.Null(FightPlay.ChallengeOf(Fight(Challenge(Zombie)), _catalog, ChallengePlay.None, risky: false, _noneLeft));
    }

    [Fact]
    public void AChallengeLeftDecidedOrUnknownIsNotPlayedFor()
    {
        Assert.Null(FightPlay.ChallengeOf(Fight(Challenge(Zombie)), _catalog, ChallengePlay.Easy, risky: false, new HashSet<int> { Zombie }));
        Assert.Null(FightPlay.ChallengeOf(Fight(Challenge(Zombie, succeeded: false)), _catalog, ChallengePlay.Easy, risky: false, _noneLeft));
        Assert.Null(FightPlay.ChallengeOf(Fight(Challenge(Zombie, succeeded: true)), _catalog, ChallengePlay.Easy, risky: false, _noneLeft));
        Assert.Null(FightPlay.ChallengeOf(Fight(Challenge(999)), _catalog, ChallengePlay.Easy, risky: false, _noneLeft));
    }
}
