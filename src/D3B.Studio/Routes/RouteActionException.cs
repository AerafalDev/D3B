namespace D3B.Studio.Routes;

/// <summary>An action of a route that could not be played: the route fails, with the reason in the bot's journal (D384).</summary>
internal sealed class RouteActionException : Exception
{
    /// <summary>Gets the action's node id.</summary>
    public string Node { get; }

    /// <summary>Gets how it ended, as the movement or the dialog said.</summary>
    public string Outcome { get; }

    /// <summary>Initializes a new instance of the <see cref="RouteActionException"/> class.</summary>
    /// <param name="node">The action's node id.</param>
    /// <param name="outcome">How it ended.</param>
    public RouteActionException(string node, string outcome)
        : base($"The route's action {node} ended {outcome}.")
    {
        Node = node;
        Outcome = outcome;
    }
}
