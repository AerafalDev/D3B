namespace D3B.Studio.Behaviors;

/// <summary>A wire on a state's canvas: of execution, from a node's way out to the next node; or of data, from a node's value to another's input (D311).</summary>
/// <param name="From">The node it leaves.</param>
/// <param name="Pin">
/// The pin it leaves by: a way out, <see cref="GraphPins.Next"/>, <see cref="GraphPins.True"/> or <see cref="GraphPins.False"/>;
/// or a value, <see cref="GraphPins.Value"/>.
/// </param>
/// <param name="To">The node it reaches.</param>
/// <param name="Input">The value it brings the node, by key; none for a wire of execution.</param>
public sealed record GraphLink(string From, string Pin, string To, string? Input = null)
{
    /// <summary>Gets whether the wire brings a value rather than the way on.</summary>
    public bool IsData => Input is not null;
}
