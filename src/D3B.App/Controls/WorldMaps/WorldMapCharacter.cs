using Avalonia.Media;

namespace D3B.App.Controls.WorldMaps;

/// <summary>A character in the world as the view draws it: its portrait's disc and its name over the map it stands on (D397).</summary>
/// <param name="Key">The character's key, which the studio selects it by.</param>
/// <param name="Name">The character's name.</param>
/// <param name="Initials">The letters of the disc.</param>
/// <param name="Color">The disc's colour, its breed's.</param>
/// <param name="X">The map's horizontal coordinate.</param>
/// <param name="Y">The map's vertical coordinate.</param>
/// <param name="IsSelected">Whether the character is the one selected in the studio.</param>
/// <param name="IsPlaying">Whether the character plays, which its ring says in green.</param>
internal sealed record WorldMapCharacter(string Key, string Name, string Initials, Color Color, int X, int Y, bool IsSelected, bool IsPlaying);
