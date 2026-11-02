using Avalonia.Media;

namespace D3B.App.ViewModels.Logs;

/// <summary>Whom a line of the journal is about, as its source column draws it: a portrait, or the host's mark (D327).</summary>
/// <param name="Name">The character's name, or the app's word for the host.</param>
/// <param name="Initials">What the portrait says without a head.</param>
/// <param name="Color">The portrait's colour, its breed's.</param>
/// <param name="Head">The class's head, once the pack and the server's list say it.</param>
internal sealed record LogWho(string Name, string Initials, IBrush Color, IImage? Head);
