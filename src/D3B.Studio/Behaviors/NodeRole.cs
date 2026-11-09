namespace D3B.Studio.Behaviors;

/// <summary>What a kind of node does in a behavior (D297, D298).</summary>
public enum NodeRole
{
    /// <summary>A short action, a goal the state says reached or not, or played whole.</summary>
    Action,

    /// <summary>One of today's activities, harvesting or hunting, whole, its settings its values (D297).</summary>
    Activity,

    /// <summary>A pure read of the state, true or false.</summary>
    Condition,

    /// <summary>Something that happened between two states of the bot, a rule's start.</summary>
    Event,

    /// <summary>A branch: the way on chosen by a condition.</summary>
    Branch,

    /// <summary>A variable set, played at once.</summary>
    SetVariable,

    /// <summary>A variable read, its value given to another node.</summary>
    GetVariable,
}
