namespace D3B.Engine.Tactics;

/// <summary>Which enemies a challenge lets the fighter playing kill (D497).</summary>
public enum ChallengeKills
{
    /// <summary>Any.</summary>
    Free,

    /// <summary>None but the target while it lives: Premier.</summary>
    TargetFirst,

    /// <summary>The target only once it is the last enemy alive: Dernier.</summary>
    TargetLast,

    /// <summary>The target alone, and none once it is dead until the server names the next: Tueur à gages.</summary>
    TargetOnly,

    /// <summary>The lowest level of the enemies alive first: Cruel.</summary>
    LowestLevelFirst,

    /// <summary>The highest level of the enemies alive first: Ordonné.</summary>
    HighestLevelFirst,
}
