namespace D3B.Bot.Fights;

/// <summary>Which of a fight's challenges the AI plays for, which a profile sets for every fight the AI plays (D466).</summary>
/// <remarks>Whatever the profile, the AI plays for none in a fight it judged too risky, and leaves one that costs a turn more than it pays.</remarks>
public enum ChallengePlay
{
    /// <summary>None: the AI plays as if the fight had no challenge.</summary>
    None,

    /// <summary>The easy ones, which ask only of the character's own turns: the owner's choice of default.</summary>
    Easy,
}
