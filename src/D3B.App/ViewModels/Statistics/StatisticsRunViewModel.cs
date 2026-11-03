using Avalonia.Media;

namespace D3B.App.ViewModels.Statistics;

/// <summary>A run the statistics list: who ran what, when, and how long or how it ended (D146, D327).</summary>
/// <param name="Title">The profile or activity it ran.</param>
/// <param name="Character">The character that ran it.</param>
/// <param name="Span">When it ran: since when, or from when to when.</param>
/// <param name="End">How long it ran, or that it goes on, or failed.</param>
/// <param name="IsLive">Whether it goes on.</param>
/// <param name="IsFailed">Whether it failed.</param>
/// <param name="Initials">What the character's portrait says without its head.</param>
/// <param name="Color">The portrait's colour, its breed's.</param>
/// <param name="Head">The class's head, once known.</param>
internal sealed record StatisticsRunViewModel(string Title, string Character, string Span, string End, bool IsLive, bool IsFailed, string Initials, IBrush Color, IImage? Head)
{
    /// <summary>Gets what the run's second line says: the character and when.</summary>
    public string Detail => Character + " · " + Span;
}
