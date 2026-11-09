namespace D3B.Studio.Behaviors.Nodes;

/// <summary>A number compared with another, such as a variable's with a bound.</summary>
public sealed class NumberValues
{
    /// <summary>Gets or sets the number, a variable's or a wire's.</summary>
    public int Value { get; set; }

    /// <summary>Gets or sets the bound.</summary>
    public int Least { get; set; }
}
