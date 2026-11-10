namespace D3B.Studio.Behaviors;

/// <summary>How many times a step, or a list, is played (D420).</summary>
public enum RepeatKind
{
    /// <summary>One pass.</summary>
    Once,

    /// <summary>A number of passes, two or more.</summary>
    Times,

    /// <summary>Passes for ever, until the run is cut or stopped.</summary>
    Forever,

    /// <summary>Passes until a condition holds, read before each one.</summary>
    Until,
}
