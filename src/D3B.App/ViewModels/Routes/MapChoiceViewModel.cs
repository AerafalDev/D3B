namespace D3B.App.ViewModels.Routes;

/// <summary>One of the maps that share coordinates, offered to choose among on a click (D299).</summary>
/// <param name="MapId">The map.</param>
/// <param name="Zone">The name of its zone.</param>
/// <param name="IsInside">Whether it is an inside, on no world map.</param>
/// <param name="Kind">What it is, said: the world's own or an inside.</param>
internal sealed record MapChoiceViewModel(long MapId, string Zone, bool IsInside, string Kind);
