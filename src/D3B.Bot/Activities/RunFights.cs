using D3B.Bot.Fights;
using D3B.Protocol.Game;

namespace D3B.Bot.Activities;

/// <summary>The fights a run's character fought while the run went on: how they ended, and what they gave it (D505).</summary>
/// <remarks>
/// Every fight that ended during the run counts, the AI's and the player's alike: the character's numbers are what judge a
/// profile. A death is the character's fighter dead as the fight ended, the rule <c>scripts/fights.cs --live</c> counts by
/// (D463), so that a fight won by the team may cost a death.
/// </remarks>
public sealed record RunFights
{
    /// <summary>Gets how many fights the character won.</summary>
    public int Won { get; init; }

    /// <summary>Gets how many fights the character lost.</summary>
    public int Lost { get; init; }

    /// <summary>Gets how many fights the character ended dead.</summary>
    public int Deaths { get; init; }

    /// <summary>Gets the experience the fights gave the character.</summary>
    public long Experience { get; init; }

    /// <summary>Gets the kamas the fights gave the character.</summary>
    public long Kamas { get; init; }

    /// <summary>Gets how many fights ended won or lost.</summary>
    public int Fought => Won + Lost;

    /// <summary>Counts one fight more.</summary>
    /// <param name="end">How the fight ended for the character.</param>
    /// <param name="died">Whether the character's fighter was dead as it ended.</param>
    /// <returns>The fights, that one counted.</returns>
    public RunFights With(FightEnd end, bool died)
    {
        ArgumentNullException.ThrowIfNull(end);

        return this with
        {
            Won = Won + (end.Outcome is FightOutcome.Won ? 1 : 0),
            Lost = Lost + (end.Outcome is FightOutcome.Lost ? 1 : 0),
            Deaths = Deaths + (died ? 1 : 0),
            Experience = Experience + end.Experience,
            Kamas = Kamas + end.Kamas,
        };
    }
}
