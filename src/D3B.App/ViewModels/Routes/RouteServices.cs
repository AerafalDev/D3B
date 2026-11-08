using D3B.Bot.Settings;
using D3B.Data.Maps;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Data.WorldMaps;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>What the route editors read from the host: the project, the world and its path, the maps by coordinates and their tiles (D299, D308).</summary>
/// <param name="Library">The project, which holds the routes.</param>
/// <param name="Planner">The world's path between maps.</param>
/// <param name="Index">The maps by coordinates.</param>
/// <param name="Catalog">The worlds the client draws and their zooms.</param>
/// <param name="Atlas">Where each map lies.</param>
/// <param name="Texts">The pack's texts, or <see langword="null"/> without them.</param>
/// <param name="ReadTile">Reads a tile's JPEG by world, zoom and number.</param>
/// <param name="Nodes">The kinds of nodes, whose sentences the actions of a step are, or <see langword="null"/> without them.</param>
/// <param name="Game">The game's values the actions name, or <see langword="null"/>.</param>
/// <param name="Maps">The maps' cells, which an action picks among, or <see langword="null"/>.</param>
/// <param name="Provider">The host's services, which the actions' previews read, or <see langword="null"/>.</param>
internal sealed record RouteServices(AssetLibrary Library, RoutePlanner Planner, WorldMapIndex Index, WorldMapCatalog Catalog, WorldAtlas Atlas, GameTexts? Texts, Func<int, int, int, byte[]> ReadTile, NodeCatalog? Nodes = null, GameReferences? Game = null, GameMaps? Maps = null, IServiceProvider? Provider = null);
