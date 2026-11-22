namespace D3B.Engine.Tactics;

/// <summary>Which enemies a challenge lets the fighter playing strike (D497).</summary>
public enum ChallengeStrikes
{
    /// <summary>Any.</summary>
    Free,

    /// <summary>None but the target while it lives: Élitiste.</summary>
    TargetOnly,

    /// <summary>None but the target, then the enemy nearest the fighter once the target is dead: Tueur à vue.</summary>
    NearestOnly,

    /// <summary>None but the enemy struck last while it lives: Focus.</summary>
    OneAtATime,
}
