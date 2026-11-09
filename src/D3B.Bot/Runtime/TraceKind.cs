namespace D3B.Bot.Runtime;

/// <summary>What a line of a run's trace says the behavior did (D327).</summary>
public enum TraceKind
{
    /// <summary>A state was chosen, the one whose conditions held first.</summary>
    Entered,

    /// <summary>No state held: the run waits for one.</summary>
    Waiting,

    /// <summary>An urgent state came to hold, and cut the step running.</summary>
    Urgent,

    /// <summary>A threshold the activity playing watches was weighed, as it came to hold or stopped holding.</summary>
    Weighed,

    /// <summary>A rule's event happened and its conditions held: its actions were played.</summary>
    Rule,

    /// <summary>A step of the state was played: an action, or a step of an activity.</summary>
    Played,
}
