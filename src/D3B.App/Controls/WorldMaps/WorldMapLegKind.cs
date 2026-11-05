namespace D3B.App.Controls.WorldMaps;

/// <summary>How a leg of the route is drawn (D299, D371).</summary>
internal enum WorldMapLegKind
{
    /// <summary>The world's path: a thin teal line over the maps it crosses.</summary>
    Path,

    /// <summary>No path leads: a red dashed line with a mark, between the two stations.</summary>
    Unreachable,
}
