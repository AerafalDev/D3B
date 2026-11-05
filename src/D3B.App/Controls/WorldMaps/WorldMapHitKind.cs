namespace D3B.App.Controls.WorldMaps;

/// <summary>What lies under the pointer on the world's map.</summary>
internal enum WorldMapHitKind
{
    /// <summary>Nothing: the sea, or outside the image.</summary>
    None,

    /// <summary>The coordinates of a map.</summary>
    Square,

    /// <summary>A station of the route.</summary>
    Station,

    /// <summary>A map the world's path crosses between two stations, which a click pins.</summary>
    PathPoint,

    /// <summary>The mark of coordinates that several maps share, which a click asks to choose among.</summary>
    Shared,
}
