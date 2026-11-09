using D3B.Studio.Behaviors;

namespace D3B.Studio.Routes;

/// <summary>An action drawn across the steps of a route that hold it, as the Sequencer shows it (D395, D396).</summary>
/// <remarks>
/// A clip is derived from the route's steps by <see cref="RouteClips.Derive"/> and never stored: the file keeps each step's
/// actions as D378 made them.
/// </remarks>
/// <param name="Call">The action, as its steps hold it.</param>
/// <param name="First">The first step it covers, from 0.</param>
/// <param name="Last">The last step it covers.</param>
/// <param name="Layer">
/// Which of the equal actions of a step it is, from 0: a step that holds the same action twice has two clips, one on each layer.
/// </param>
/// <param name="Lane">The row of its track it is drawn on, from 0: clips of one kind that overlap stand one above the other.</param>
public sealed record RouteClip(NodeCall Call, int First, int Last, int Layer, int Lane)
{
    /// <summary>Gets how many steps the clip covers.</summary>
    public int Length => Last - First + 1;

    /// <summary>Tells whether the clip covers a step.</summary>
    /// <param name="step">The step, from 0.</param>
    /// <returns>Whether the step is in the clip's range.</returns>
    public bool Covers(int step)
    {
        return step >= First && step <= Last;
    }
}
