using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Characters;
using D3B.Storage.Accounts;

namespace D3B.App.ViewModels.Characters;

/// <summary>A character offline, a line under its heading: its portrait dimmed, its name, its level and when it last played (D323).</summary>
internal sealed partial class CharacterLineViewModel : ObservableObject
{
    private readonly CharactersViewModel _panel;

    /// <summary>Gets the character.</summary>
    public CharacterKey Key { get; }

    /// <summary>Gets the character's name.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets the first letters of its name, its portrait without a head.</summary>
    [ObservableProperty]
    public partial string Initials { get; private set; } = "";

    /// <summary>Gets its level, said, if the server listed it.</summary>
    [ObservableProperty]
    public partial string Level { get; private set; } = "";

    /// <summary>Gets its class's head, if known and in the pack.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHead))]
    public partial IImage? Head { get; private set; }

    /// <summary>Gets its portrait's colour, its breed's.</summary>
    [ObservableProperty]
    public partial IBrush PortraitColor { get; private set; } = BreedColors.Of(null);

    /// <summary>Gets when it last played, or that it is disconnected.</summary>
    [ObservableProperty]
    public partial string LastPlayed { get; private set; } = "";

    /// <summary>Gets or sets whether the run block and the inspector show it.</summary>
    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    /// <summary>Gets whether its line shows its class's head.</summary>
    public bool HasHead => Head is not null;

    /// <summary>Gets the panel, whose menu the line's right click opens.</summary>
    public CharactersViewModel Panel => _panel;

    /// <summary>Initializes a new instance of the <see cref="CharacterLineViewModel"/> class.</summary>
    /// <param name="panel">The characters' panel.</param>
    /// <param name="key">The character.</param>
    public CharacterLineViewModel(CharactersViewModel panel, CharacterKey key)
    {
        _panel = panel;
        Key = key;
    }

    /// <summary>Reads the character again, as D3B knows it and its bot, if one was made, last saw it.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="known">What D3B knows of it, if it saw it.</param>
    /// <param name="bot">Its bot, if the host made one.</param>
    /// <param name="icons">The pack's icons, if the host has a pack.</param>
    /// <param name="now">The time now.</param>
    public void Refresh(Localizer localizer, KnownCharacter? known, BotViewModel? bot, GameIcons? icons, DateTimeOffset now)
    {
        Name = bot?.Name ?? (known is { Name.Length: > 0 } ? known.Name : localizer.Format("CharacterNamed", Key.CharacterId));
        Initials = AvatarInitials.Of(Name);
        Level = known?.Level is { } level ? localizer.Format("Level", level) : bot?.Level ?? "";
        Head = known?.BreedId is { } breed ? icons?.Head(breed, known.IsFemale ?? false) : null;
        PortraitColor = BreedColors.Of(known?.BreedId);
        LastPlayed = known?.LastPlayed is { } last ? Ago.Of(localizer, "Ago", last, now) : localizer["Disconnected"];
    }

    [RelayCommand]
    private void Choose()
    {
        _panel.Choose(Key);
    }

    [RelayCommand]
    private void Open()
    {
        _panel.Open(Key);
    }
}
