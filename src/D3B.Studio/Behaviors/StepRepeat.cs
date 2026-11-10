namespace D3B.Studio.Behaviors;

/// <summary>How a step of a list, or the list itself, is repeated: once, a number of times, for ever or until a condition holds (D420).</summary>
/// <remarks>
/// A pass is the step played to its end: a route followed once through its course (a loop's lap), a sequence played once. The
/// condition of an <see cref="RepeatKind.Until"/> is read before each pass, the first included, and never in the middle of one.
/// </remarks>
public sealed class StepRepeat
{
    /// <summary>The most passes a count can ask for.</summary>
    public const int MostTimes = 999;

    /// <summary>Gets one pass, what a step does when nothing is said.</summary>
    public static StepRepeat Once { get; } = new(RepeatKind.Once, 1, null);

    /// <summary>Gets passes for ever.</summary>
    public static StepRepeat Forever { get; } = new(RepeatKind.Forever, 0, null);

    /// <summary>Gets how the steps are repeated.</summary>
    public RepeatKind Kind { get; }

    /// <summary>Gets how many passes a <see cref="RepeatKind.Times"/> asks for, and one for <see cref="RepeatKind.Once"/>.</summary>
    public int Count { get; }

    /// <summary>Gets the condition of an <see cref="RepeatKind.Until"/>: a node of the catalog that reads the state.</summary>
    public NodeCall? Condition { get; }

    private StepRepeat(RepeatKind kind, int count, NodeCall? condition)
    {
        Kind = kind;
        Count = count;
        Condition = condition;
    }

    /// <summary>Makes a repeat of a number of passes.</summary>
    /// <param name="count">How many passes, between one and <see cref="MostTimes"/>.</param>
    /// <returns>The repeat; <see cref="Once"/> for one pass.</returns>
    public static StepRepeat Times(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MostTimes);

        return count == 1 ? Once : new StepRepeat(RepeatKind.Times, count, null);
    }

    /// <summary>Makes a repeat that goes on until a condition holds.</summary>
    /// <param name="condition">The condition's node and values.</param>
    /// <returns>The repeat.</returns>
    public static StepRepeat Until(NodeCall condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return new StepRepeat(RepeatKind.Until, 0, condition);
    }

    /// <summary>Tells whether two repeats ask for the same passes, with the same condition and values.</summary>
    /// <param name="other">The other repeat.</param>
    /// <returns>Whether they are the same.</returns>
    public bool IsSame(StepRepeat other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Kind == other.Kind && Count == other.Count && (Condition is null ? other.Condition is null : other.Condition is not null && Condition.IsSame(other.Condition));
    }
}
