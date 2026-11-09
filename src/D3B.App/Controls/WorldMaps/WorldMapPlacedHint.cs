using Avalonia;

namespace D3B.App.Controls.WorldMaps;

/// <summary>A hint the view keeps at the zoom it shows, and where its picture lies in the view (D414).</summary>
/// <param name="Hint">The hint.</param>
/// <param name="Bounds">The picture's square, in the view's own.</param>
internal readonly record struct WorldMapPlacedHint(WorldMapHint Hint, Rect Bounds);
