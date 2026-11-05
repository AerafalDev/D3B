using D3B.Data.WorldMaps;

namespace D3B.App.Controls.WorldMaps;

/// <summary>Where the world's map reads from: the worlds, their maps by coordinates, and their tiles.</summary>
/// <param name="Catalog">The worlds and their zooms.</param>
/// <param name="Index">The maps by coordinates.</param>
/// <param name="ReadTile">Reads a tile's JPEG by world, zoom and number; called off the interface's thread.</param>
internal sealed record WorldMapSource(WorldMapCatalog Catalog, WorldMapIndex Index, Func<int, int, int, byte[]> ReadTile);
