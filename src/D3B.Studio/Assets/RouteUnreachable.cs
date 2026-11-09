namespace D3B.Studio.Assets;

/// <summary>What a route does with a map no path reaches (D299).</summary>
public enum RouteUnreachable
{
    /// <summary>Passes it and goes on to the next anchor.</summary>
    Skip,

    /// <summary>Fails the route.</summary>
    Fail,
}
