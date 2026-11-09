namespace D3B.Studio.Assets;

/// <summary>How a route goes through its anchors (D299).</summary>
public enum RouteMode
{
    /// <summary>From the last anchor back to the first, and again.</summary>
    Loop,

    /// <summary>From the first to the last, once.</summary>
    OneWay,

    /// <summary>From the first to the last, then back to the first.</summary>
    RoundTrip,
}
