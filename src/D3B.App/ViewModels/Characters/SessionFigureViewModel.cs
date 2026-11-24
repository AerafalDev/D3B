using Avalonia.Media;

namespace D3B.App.ViewModels.Characters;

/// <summary>A figure of a character's session that is not an item, such as the experience its fights gave: its glyph, how much, and by hour (D505).</summary>
/// <param name="Glyph">The figure's glyph, if the pack holds it.</param>
/// <param name="Name">What it counts.</param>
/// <param name="Count">How much, said.</param>
/// <param name="Rate">How much by hour, said.</param>
internal sealed record SessionFigureViewModel(IImage? Glyph, string Name, string Count, string Rate);
