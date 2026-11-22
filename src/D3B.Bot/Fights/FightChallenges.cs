using D3B.Data.Challenges;
using D3B.Engine.Effects;
using D3B.Engine.Tactics;

namespace D3B.Bot.Fights;

/// <summary>What a fight's challenge asks of a fighter's turn, with what the server and the fight said of it so far (D466, D497).</summary>
public static class FightChallenges
{
    /// <summary>Gets what a challenge asks of a fighter's turn, its target and what the fight bound it to filled in.</summary>
    /// <param name="fight">The fight, as the bot knows it.</param>
    /// <param name="challenge">The challenge.</param>
    /// <param name="catalog">The client's challenges, whose completion criteria say what each asks.</param>
    /// <param name="fighterId">The fighter whose turn it is.</param>
    /// <returns>What it asks; null when it is not one the AI plays for.</returns>
    /// <remarks>
    /// The target is the first the server named whose fighter is alive; for Tueur à vue, the one it named for the fighter, as
    /// it names one an attacker (D461). Élémentaire's element is the one the fighter's first blow struck in, and Focus's enemy
    /// the one its team struck last, while it lives (D497).
    /// </remarks>
    public static TurnChallenge? TurnOf(FightState fight, FightChallenge challenge, ChallengeCatalog catalog, long fighterId)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(catalog);

        if (TurnChallenge.Of(catalog.CompletionOf(challenge.Id), challenge.Bonus) is not { } turn)
            return null;

        var living = challenge.Targets.Where(target => fight.Fighters.GetValueOrDefault(target.FighterId) is not { IsAlive: false }).ToList();
        var target = turn.Strikes is ChallengeStrikes.NearestOnly ? living.Find(target => target.AttackerIds.Contains(fighterId)) : living.FirstOrDefault();

        // What the fight bound the challenge to so far: the element of the fighter's first blow, the enemy its team struck last.
        var element = turn.SameElement && fight.FirstElements.TryGetValue(fighterId, out var first) ? (EffectElement)first : (EffectElement?)null;
        var focus = turn.Strikes is ChallengeStrikes.OneAtATime && fight.StruckEnemyId is { } struck && fight.Fighters.GetValueOrDefault(struck) is { IsAlive: true } ? struck : (long?)null;

        return turn with { TargetId = target?.FighterId, Element = element, FocusId = focus };
    }
}
