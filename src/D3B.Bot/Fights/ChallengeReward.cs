namespace D3B.Bot.Fights;

/// <summary>What a fight's challenges won give, which a profile sets for every fight the AI plays (D438).</summary>
/// <remarks>The game keeps one choice per team, its leader's: the bot sends it during the placement when its character leads.</remarks>
public enum ChallengeReward
{
    /// <summary>More drops: the owner's choice of default.</summary>
    Drop,

    /// <summary>More experience.</summary>
    Experience,

    /// <summary>Nothing sent: the choice the client or the player made stays.</summary>
    Client,
}
