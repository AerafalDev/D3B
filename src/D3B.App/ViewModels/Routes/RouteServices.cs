using D3B.Bot.Assets;
using D3B.Bot.Routes;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Data.WorldMaps;

namespace D3B.App.ViewModels.Routes;

/// <summary>What the route editors read from the host: the project, the world and its path, the maps by coordinates and their tiles (D299, D308).</summary>
/// <param name="Library">The project, which holds the routes.</param>
/// <param name="Planner">The world's path between maps.</param>
/// <param name="Index">The maps by coordinates.</param>
/// <param name="Catalog">The worlds the client draws and their zooms.</param>
/// <param name="Atlas">Where each map lies.</param>
/// <param name="Texts">The pack's texts, or <see langword="null"/> without them.</param>
/// <param name="ReadTile">Reads a tile's JPEG by world, zoom and number.</param>
internal sealed record RouteServices(AssetLibrary Library, RoutePlanner Planner, WorldMapIndex Index, WorldMapCatalog Catalog, WorldAtlas Atlas, GameTexts? Texts, Func<int, int, int, byte[]> ReadTile);
