using D3B.Bot.Characters;
using D3B.Bot.Localization;

namespace D3B.Bot.Activities;

/// <summary>A character over its pods, which a profile going on full keeps playing with rather than banking (D578).</summary>
internal static class FullPods
{
    /// <summary>Tells whether the character carries as much as its pods allow, or more.</summary>
    /// <param name="state">The character's state.</param>
    /// <returns>Whether it is full; never while its pods are not known.</returns>
    public static bool Holds(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.MaxWeight > 0 && state.Weight >= state.MaxWeight;
    }

    /// <summary>Gets what the card of an activity going on full warns of, once the character is full.</summary>
    /// <param name="state">The character's state.</param>
    /// <returns>The warning; null while the character is not full.</returns>
    public static GameText? WarningOf(CharacterState state)
    {
        return Holds(state) ? GameText.Of(ActivityTexts.Text("WarningFull"), GameTextValue.Number(state.Weight), GameTextValue.Number(state.MaxWeight)) : null;
    }
}
