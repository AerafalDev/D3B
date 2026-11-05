namespace D3B.App.Controls.WorldMaps;

/// <summary>A tile of a world's image: its world, its zoom and its number in the zoom (D369).</summary>
/// <param name="World">The world.</param>
/// <param name="ZoomPercent">The zoom, such as 75.</param>
/// <param name="Index">The tile's number in the zoom, from 1.</param>
internal readonly record struct WorldMapTileKey(int World, int ZoomPercent, int Index);
