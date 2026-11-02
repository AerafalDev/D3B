using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace D3B.App.ViewModels.Characters;

/// <summary>The colour of each breed's portrait, under its class's head or its initials (D323).</summary>
/// <remarks>
/// Muted, as the studio's states alone are bright (D320); a breed the client may add later takes the neutral grey. The
/// brushes are immutable: shared by every view model, they are made on whichever thread asks first.
/// </remarks>
internal static class BreedColors
{
    private static readonly IBrush _neutral = new ImmutableSolidColorBrush(Color.Parse("#4A4B55"));

    // By breed id, the client's table: Féca, Osamodas, Enutrof, Sram, Xélor, Ecaflip, Eniripsa, Iop, Crâ, Sadida,
    // Sacrieur, Pandawa, Roublard, Zobal, Steamer, Eliotrope, Huppermage, Ouginak, then 19, gone from 3.7.12.12, and Forgelance.
    private static readonly IBrush[] _breeds =
    [
        .. new[]
        {
            "#A6884A", "#4E8A63", "#A4863A", "#5D5F8C", "#4A6FC4", "#B8653E", "#3A9B7A", "#B8573A", "#4F86A8", "#5E8F3E",
            "#A8423F", "#6E7A86", "#7A5A9E", "#8E5470", "#3F7F8C", "#3F8F9E", "#6474C4", "#8C6A44", "#4A4B55", "#7C8A9C",
        }.Select(color => new ImmutableSolidColorBrush(Color.Parse(color))),
    ];

    /// <summary>Gets a breed's colour.</summary>
    /// <param name="breedId">The breed, or <see langword="null"/> when unknown.</param>
    /// <returns>Its colour, or the neutral grey.</returns>
    public static IBrush Of(int? breedId)
    {
        return breedId is >= 1 and <= 20 ? _breeds[breedId.Value - 1] : _neutral;
    }
}
