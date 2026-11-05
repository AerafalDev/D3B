namespace D3B.App.Controls.WorldMaps;

/// <summary>What a press on the world's map does (D371).</summary>
internal enum WorldMapTool
{
    /// <summary>Chooses a station, moves it, pins a point of the path, opens the maps that share coordinates, else pans.</summary>
    Select,

    /// <summary>Adds an anchor on the map pressed, and on each one a drag crosses.</summary>
    Trace,
}
