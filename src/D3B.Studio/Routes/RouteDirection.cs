namespace D3B.Studio.Routes;

/// <summary>A side of a map, which a forced exit leaves by (D377).</summary>
public enum RouteDirection
{
    /// <summary>The top of the map, towards the map north of it.</summary>
    Top,

    /// <summary>The right of the map, towards the map east of it.</summary>
    Right,

    /// <summary>The bottom of the map, towards the map south of it.</summary>
    Bottom,

    /// <summary>The left of the map, towards the map west of it.</summary>
    Left,
}
