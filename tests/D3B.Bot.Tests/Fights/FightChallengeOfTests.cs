using System.Collections.Immutable;
using D3B.Bot.Fights;
using D3B.Data.Challenges;
using D3B.Engine.Effects;
using D3B.Engine.Tactics;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Fights;

/// <summary>The challenge a turn plays for: the fight's first easy one not yet decided nor left, never in a risky fight (D466).</summary>
public sealed class FightChallengeOfTests
{
    private const int Zombie = 1;
    private const int First = 3;
    private const int Barbarian = 9;
    private const int Elemental = 20;
    private const int Focus = 31;
    private const int Prudent = 40;
    private const int Sight = 967;
    private const long Self = 7;

    private static readonly int[] _none = [];
    private static readonly string[] _completions = ["TM=1,0", "CK~1", "Ma=1", "SC=hc0,e,s", "SF=1", "TD>1,hc0,e1", "CO=e0,m1,tk,n"];
    private static readonly string[] _activations = ["GL>4,0", "GL>4,0", "GL>4,0", "GL>4,0", "GL>4,0", "GL>4,0", "GL>4,0"];
    private static readonly HashSet<int> _noneLeft = [];

    private static readonly ChallengeCatalog _catalog = new(
        new Challenge[]
        {
            new(Zombie, 742, 1, 1, 1, 0, 0, 0), new(First, 851_058, 3, 1, 3, 0, 0, 0), new(Barbarian, 851_059, 9, 1, 9, 0, 0, 0),
            new(Elemental, 851_060, 20, 1, 20, 0, 0, 0), new(Focus, 851_061, 31, 1, 31, 0, 0, 0),
            new(Prudent, 851_067, 40, 1, 40, 0, 0, 0), new(Sight, 851_070, 967, 1, 967, 0, 0, 0),
        },
        _none,
        _completions,
        _activations);

    private static FightState Fight(params FightChallenge[] challenges)
    {
        return new FightState { Challenges = [.. challenges] };
    }

    private static FightChallenge Challenge(int id, bool? succeeded = null, params FightChallengeTarget[] targets)
    {
        return new FightChallenge(id, 100, [.. targets], succeeded);
    }

    private static (int Id, TurnChallenge Challenge)? Of(FightState fight, ChallengePlay play = ChallengePlay.Easy, bool risky = false, IReadOnlySet<int>? left = null)
    {
        return FightPlay.ChallengeOf(fight, _catalog, play, risky, left ?? _noneLeft, Self);
    }

    [Fact]
    public void AnEasyChallengeIsPlayedForWithWhatItsCriterionAsks()
    {
        var played = Of(Fight(Challenge(Zombie)));

        Assert.NotNull(played);
        Assert.Equal(Zombie, played.Value.Id);
        Assert.Equal(TurnMovement.OnePoint, played.Value.Challenge.Movement);
        Assert.Equal(0.375, played.Value.Challenge.MostLoss, 6);
    }

    [Fact]
    public void AChallengeThatIsNotEasyIsNotPlayedFor()
    {
        Assert.Null(Of(Fight(Challenge(Barbarian))));
        Assert.Equal(Prudent, Of(Fight(Challenge(Barbarian), Challenge(Prudent)))?.Id);
    }

    [Fact]
    public void NoneIsPlayedForInARiskyFightOrWithAProfileThatPlaysForNone()
    {
        // The fight's safety first: a challenge never loses a fight.
        Assert.Null(Of(Fight(Challenge(Zombie)), risky: true));
        Assert.Null(Of(Fight(Challenge(Zombie)), ChallengePlay.None));
    }

    [Fact]
    public void AChallengeLeftDecidedOrUnknownIsNotPlayedFor()
    {
        Assert.Null(Of(Fight(Challenge(Zombie)), left: new HashSet<int> { Zombie }));
        Assert.Null(Of(Fight(Challenge(Zombie, succeeded: false))));
        Assert.Null(Of(Fight(Challenge(Zombie, succeeded: true))));
        Assert.Null(Of(Fight(Challenge(999))));
    }

    [Fact]
    public void AChallengeOfATargetAimsAtTheFighterTheServerNamed()
    {
        Assert.Equal(-5, Of(Fight(Challenge(First, null, new FightChallengeTarget(-5, []))))?.Challenge.TargetId);
    }

    [Fact]
    public void TueurAVueAimsAtTheTargetTheServerNamedForTheCharacter()
    {
        var sight = Challenge(Sight, null, new FightChallengeTarget(-5, [8]), new FightChallengeTarget(-6, [Self]));

        Assert.Equal(-6, Of(Fight(sight))?.Challenge.TargetId);
    }

    [Fact]
    public void ElementaireHoldsTheFighterToTheElementOfItsFirstBlow()
    {
        var fight = Fight(Challenge(Elemental)) with { FirstElements = ImmutableSortedDictionary<long, int>.Empty.Add(8, 3).Add(Self, 2) };

        Assert.Equal(EffectElement.Fire, Of(fight)?.Challenge.Element);
        Assert.Null(Of(Fight(Challenge(Elemental)))?.Challenge.Element);
    }

    [Fact]
    public void FocusHoldsTheTeamToTheEnemyItStruckWhileItLives()
    {
        var alive = new Fighter { Id = -5, Team = FightTeam.Defender, IsAlive = true };
        var fight = Fight(Challenge(Focus)) with { StruckEnemyId = -5, Fighters = ImmutableSortedDictionary<long, Fighter>.Empty.Add(-5, alive) };

        Assert.Equal(-5, Of(fight)?.Challenge.FocusId);
        Assert.Null(Of(fight with { Fighters = fight.Fighters.SetItem(-5, alive with { IsAlive = false }) })?.Challenge.FocusId);
    }

    [Fact]
    public void BeforeEachPlanTheTurnReadsWhatItsCastsBoundTheChallengeToKeepingItsPrice()
    {
        // The turn's first cast struck in fire: its next plans strike in fire, at the price the turn already paid.
        var played = (Elemental, TurnChallenge.Of("SC=hc0,e,s", 100)!.KeptThisTurn() with { StartCell = 300 });
        var struck = Fight(Challenge(Elemental)) with { FirstElements = ImmutableSortedDictionary<long, int>.Empty.Add(Self, 2) };

        var refreshed = FightPlay.Refreshed(played, struck, _catalog, Self);

        Assert.Equal(EffectElement.Fire, refreshed?.Challenge.Element);
        Assert.Equal(double.PositiveInfinity, refreshed?.Challenge.MostLoss);
        Assert.Equal(300, refreshed?.Challenge.StartCell);
    }

    [Fact]
    public void AChallengeTheServerDecidedDuringTheTurnIsNoLongerPlayedFor()
    {
        var played = (First, TurnChallenge.Of("CK~1", 100)!.KeptThisTurn());

        Assert.Null(FightPlay.Refreshed(played, Fight(Challenge(First, succeeded: false)), _catalog, Self));
        Assert.Equal(played, FightPlay.Refreshed(played, Fight(), _catalog, Self));
    }
}
