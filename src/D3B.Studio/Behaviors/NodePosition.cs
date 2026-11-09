namespace D3B.Studio.Behaviors;

/// <summary>Where a node stands on its state's canvas, apart from what it does (D294).</summary>
/// <param name="X">From the left.</param>
/// <param name="Y">From the top.</param>
public readonly record struct NodePosition(double X, double Y);
