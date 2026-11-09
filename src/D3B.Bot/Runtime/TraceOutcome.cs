namespace D3B.Bot.Runtime;

/// <summary>How a line of a run's trace ended (D327).</summary>
public enum TraceOutcome
{
    /// <summary>Nothing to say: a state entered, a run that waits.</summary>
    None,

    /// <summary>A threshold weighed holds.</summary>
    Holds,

    /// <summary>A threshold weighed does not hold.</summary>
    DoesNotHold,

    /// <summary>A step played, the action or activity going on.</summary>
    Played,

    /// <summary>A step played to its end: the action or activity is done.</summary>
    Done,

    /// <summary>A step cut, by an urgent state, a variable, a pause, the run's stop or an interruption.</summary>
    Interrupted,

    /// <summary>A step or a rule that threw.</summary>
    Failed,
}
