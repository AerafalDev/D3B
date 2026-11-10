using Avalonia.Media;

namespace D3B.App.Controls.WorldMaps;

/// <summary>A character following a route, as the map marks it: a ring on the step it goes to and, while it walks there, the leg from the step before (D419).</summary>
/// <param name="Key">The character's key.</param>
/// <param name="Initials">The letters of the marker.</param>
/// <param name="Color">The marker's colour, the character's breed's.</param>
/// <param name="X">The horizontal coordinate of the step's map.</param>
/// <param name="Y">The vertical coordinate of the step's map.</param>
/// <param name="FromX">The horizontal coordinate of the step the character comes from, or <see langword="null"/> at the first.</param>
/// <param name="FromY">The vertical coordinate of the step the character comes from, or <see langword="null"/> at the first.</param>
/// <param name="Arrived">Whether the character is on the step's map, the ring then filled; hollow while it walks there.</param>
internal sealed record WorldMapRun(string Key, string Initials, Color Color, int X, int Y, int? FromX, int? FromY, bool Arrived);
