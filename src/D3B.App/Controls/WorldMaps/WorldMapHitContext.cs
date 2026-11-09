using Avalonia;
using D3B.Data.WorldMaps;

namespace D3B.App.Controls.WorldMaps;

/// <summary>What the map's hit test needs of the view at the moment of a pointer move.</summary>
/// <param name="World">The world shown.</param>
/// <param name="Viewport">Where the view looks at the image, and how large.</param>
/// <param name="Scene">What is drawn over the image.</param>
/// <param name="Tool">What a press does.</param>
/// <param name="ShowShared">Whether the mark of coordinates that several maps share shows.</param>
/// <param name="Layer">The layer shown.</param>
/// <param name="Source">Where the maps are read from.</param>
/// <param name="RectOf">What gives the rectangle of the map at coordinates, in the view's own.</param>
internal readonly record struct WorldMapHitContext(WorldMap World, WorldMapViewport Viewport, WorldMapScene Scene, WorldMapTool Tool, bool ShowShared, int Layer, WorldMapSource Source, Func<int, int, Rect> RectOf);
