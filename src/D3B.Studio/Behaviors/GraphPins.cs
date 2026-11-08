namespace D3B.Studio.Behaviors;

/// <summary>The pins nodes leave by (D311).</summary>
public static class GraphPins
{
    /// <summary>The way on, once an action is played or passed.</summary>
    public const string Next = "next";

    /// <summary>A branch's way when its condition holds.</summary>
    public const string True = "true";

    /// <summary>A branch's way when its condition does not hold.</summary>
    public const string False = "false";

    /// <summary>The value a condition or a variable's read gives.</summary>
    public const string Value = "value";
}
