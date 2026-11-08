namespace D3B.Studio.Behaviors;

/// <summary>A branch's value: the condition that chooses its way, a wire from a condition's node or a variable.</summary>
public sealed class BranchValues
{
    /// <summary>Gets or sets whether the way is <see cref="GraphPins.True"/>'s.</summary>
    public bool Condition { get; set; }
}
