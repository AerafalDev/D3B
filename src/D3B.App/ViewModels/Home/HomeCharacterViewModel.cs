using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using D3B.App.ViewModels.Characters;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.Home;

/// <summary>A character's card on the home page: its portrait with a dot of its state, its name, what it does or that it is disconnected, its profile and a button to play or pause it; a click opens its tab (D406, D514).</summary>
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

    /// <summary>Gets whether the character is connected, so that its card offers to play it.</summary>
    public bool IsOnline => !IsDimmed;

    /// <summary>Gets the profile it plays, or that it has none; empty while it is disconnected.</summary>
    public string Profile { get; init; } = "";

    /// <summary>Gets whether it plays now: its button pauses it.</summary>
    public bool IsPlaying { get; init; }

    /// <summary>Gets whether its button may start its profile or resume it.</summary>
    public bool CanPlay { get; init; }

    /// <summary>Gets what its button does, or why it cannot.</summary>
    public string PlayTip { get; init; } = "";

    /// <summary>Gets whether its button does something: it plays, or play is allowed.</summary>
    public bool CanPlayOrPause => IsPlaying || CanPlay;

    /// <summary>Initializes a new instance of the <see cref="HomeCharacterViewModel"/> class.</summary>
    /// <param name="characters">The characters' panel, which chooses and opens them.</param>
    /// <param name="key">The character.</param>
    public HomeCharacterViewModel(CharactersViewModel characters, CharacterKey key)
    {
        _characters = characters;
        Key = key;
    }

    // Through the run block, which then drives this character: F5, F6 and the title row follow what the card did.
    [RelayCommand]
    private void PlayOrPause()
    {
        if (_characters.RunBlock is not { } run || !CanPlayOrPause)
            return;

        run.Drive(Key);
        run.PlayOrPause();
    }

    [RelayCommand]
    private void Open()
    {
        _characters.Open(Key);
    }
}
