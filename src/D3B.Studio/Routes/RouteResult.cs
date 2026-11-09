namespace D3B.Studio.Routes;

/// <summary>How a go at a route ended (D379).</summary>
public enum RouteResult
{
    /// <summary>The run went through the route as its course asks.</summary>
    Done,

    /// <summary>The route failed: a map no path reaches and the route says to fail, or an action it cannot play.</summary>
    Failed,

    /// <summary>The character could not act for a while; the run goes on from the same place when it is played again.</summary>
    Busy,
}
