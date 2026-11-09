using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using D3B.App.ViewModels.Characters;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.Home;

/// <summary>A character on the home page: its portrait with a dot of its state, its name and what it does, or that it is disconnected (D406).</summary>
internal sealed partial class HomeCharacterViewModel
{
    private readonly CharactersViewModel _characters;

    /// <summary>Gets the character.</summary>
    public CharacterKey Key { get; }

    /// <summary>Gets the character's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the first letters of its name, its portrait without a head.</summary>
    public required string Initials { get; init; }

    /// <summary>Gets what it does, or that it is disconnected.</summary>
    public required string Detail { get; init; }

    /// <summary>Gets its class's head, if known and in the pack.</summary>
    public IImage? Head { get; init; }

    /// <summary>Gets its portrait's colour, its breed's.</summary>
    public required IBrush PortraitColor { get; init; }

    /// <summary>Gets the dot of its state, or <see langword="null"/> when it is not connected.</summary>
    public IBrush? StateBrush { get; init; }

    /// <summary>Gets whether the character is disconnected: its row is dimmed.</summary>
    public bool IsDimmed { get; init; }

    /// <summary>Initializes a new instance of the <see cref="HomeCharacterViewModel"/> class.</summary>
    /// <param name="characters">The characters' panel, which chooses and opens them.</param>
    /// <param name="key">The character.</param>
    public HomeCharacterViewModel(CharactersViewModel characters, CharacterKey key)
    {
        _characters = characters;
        Key = key;
    }

    [RelayCommand]
    private void Choose()
    {
        _characters.Choose(Key);
    }

    [RelayCommand]
    private void Open()
    {
        _characters.Open(Key);
    }
}
