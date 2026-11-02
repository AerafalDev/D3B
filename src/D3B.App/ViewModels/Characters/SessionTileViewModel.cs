using Avalonia.Media;

namespace D3B.App.ViewModels.Characters;

/// <summary>A resource of a character's session as a tile: its icon, how many, its name and its rate an hour (D325).</summary>
/// <param name="Icon">The item's icon, if the pack holds it.</param>
/// <param name="Count">How many, said.</param>
/// <param name="Detail">Its name and rate an hour.</param>
internal sealed record SessionTileViewModel(IImage? Icon, string Count, string Detail);
