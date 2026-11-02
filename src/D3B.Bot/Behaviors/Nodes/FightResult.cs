namespace D3B.Bot.Behaviors.Nodes;

/// <summary>How a fight ended for the character, as a condition or an event asks it.</summary>
public enum FightResult
{
    /// <summary>Won or lost.</summary>
    Any,

    /// <summary>Won.</summary>
    Won,

    /// <summary>Lost.</summary>
    Lost,
}
