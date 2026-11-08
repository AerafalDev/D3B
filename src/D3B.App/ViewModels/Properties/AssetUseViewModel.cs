using D3B.Bot.Characters;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Properties;

/// <summary>An asset or a character an asset uses, or that uses it, as the inspector lists it (D324).</summary>
/// <param name="Name">Its name.</param>
/// <param name="Detail">What the inspector says beside it: its kind, the values changed, or that it starts on connection.</param>
/// <param name="Kind">The asset's kind, or <see langword="null"/> for a character.</param>
/// <param name="Character">The character, when one plays the profile.</param>
internal sealed record AssetUseViewModel(string Name, string Detail, AssetKind? Kind, CharacterKey? Character)
{
    /// <summary>Gets whether it is a character.</summary>
    public bool IsCharacter => Character is not null;
}
