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

    /// <summary>A character of the world's backdrop (D397).</summary>
    Character,

    /// <summary>A step of a route of the world's backdrop (D397).</summary>
    RouteStep,

    /// <summary>A map of a zone of the world's backdrop (D397).</summary>
    Zone,

    /// <summary>A point of interest the world map draws, which is pressed as the map it stands on is (D414).</summary>
    Hint,
}
