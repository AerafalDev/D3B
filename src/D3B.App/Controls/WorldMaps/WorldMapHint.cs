using Avalonia.Media;

namespace D3B.App.Controls.WorldMaps;

/// <summary>A point of interest of the world as the view draws it: the client's own picture over the map it stands on (D414).</summary>
/// <param name="Id">The client's identifier of the hint, which the view names a hit by.</param>
/// <param name="Name">The hint's name, in the language of the app.</param>
/// <param name="Category">The name of its category, such as workshops or dungeons.</param>
/// <param name="Level">The level of a dungeon, and 0 for what has none.</param>
/// <param name="X">The map's horizontal coordinate.</param>
/// <param name="Y">The map's vertical coordinate.</param>
/// <param name="Priority">How useful the hint is among those that crowd a place: the more useful is kept, and shows from a smaller zoom.</param>
/// <param name="Icon">Gives the client's picture of it, made when first drawn, or <see langword="null"/> when the pack keeps none and the view draws a plain mark.</param>
internal sealed record WorldMapHint(int Id, string Name, string Category, int Level, int X, int Y, int Priority, Func<IImage?>? Icon);
