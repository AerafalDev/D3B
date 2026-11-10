using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.App.ViewModels.Assets;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.World;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Home;

/// <summary>The home page, the studio's first tab: quick actions, the characters and the project's assets changed last (D406).</summary>
/// <remarks>
/// <para>
/// The world is not the first thing the studio shows: it appears once a route or a zone is selected (D406). What a person
/// finds here is where to start, so the rows are the characters' and the assets' own and open them where they are
/// edited. The lists are read every 250 ms with the rest of the window and replaced only when they differ, so that a
/// row being pressed stays.
/// </para>
/// <para>An asset's time is its file's: read again when the project's version moves, not at each refresh.</para>
/// </remarks>
internal sealed partial class HomeViewModel : ObservableObject
{
    private const int RecentCount = 6;

    private readonly Localizer _localizer;
    private readonly CharactersViewModel _characters;
    private readonly Func<AssetLibrary?> _library;
    private readonly Action<string> _open;
    private readonly TimeProvider _time;
    private List<(Asset Asset, DateTimeOffset Changed)> _stamped = [];
    private AssetLibrary? _read;
    private int _version = -1;
    private string? _charactersSignature;
    private string? _recentsSignature;

    /// <summary>Gets the characters, online first.</summary>
    public ObservableCollection<HomeCharacterViewModel> Characters { get; } = [];

    /// <summary>Gets the project's assets changed last, the newest first.</summary>
    public ObservableCollection<HomeRecentViewModel> Recents { get; } = [];

    /// <summary>Gets the notice that the game sessions are recorded, or <see langword="null"/> when there is none to give.</summary>
    public CapturesNoticeViewModel? Notice { get; init; }

    /// <summary>Gets or sets what makes a profile: a dialog asks its name and activity.</summary>
    public Action? ProfileRequested { get; set; }

    /// <summary>Gets or sets what makes a route and shows it on the world's map.</summary>
    public Func<Task>? RouteRequested { get; set; }

    /// <summary>Gets or sets what makes a zone and shows it on the world's map.</summary>
    public Func<Task>? ZoneRequested { get; set; }

    /// <summary>Gets or sets what shows the settings.</summary>
    public Action? SettingsRequested { get; set; }

    /// <summary>Gets or sets what opens the project's folder.</summary>
    public Action? FolderRequested { get; set; }

    /// <summary>Gets whether any character is listed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCharacters))]
    public partial bool HasCharacters { get; private set; }

    /// <summary>Gets whether any asset is listed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRecents))]
    public partial bool HasRecents { get; private set; }

    /// <summary>Gets whether no character is listed.</summary>
    public bool HasNoCharacters => !HasCharacters;

    /// <summary>Gets whether no asset is listed.</summary>
    public bool HasNoRecents => !HasRecents;

    /// <summary>Initializes a new instance of the <see cref="HomeViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="characters">The characters, online and not.</param>
    /// <param name="library">The project's assets while the host runs, else <see langword="null"/>.</param>
    /// <param name="open">Opens an asset in the editor that edits it.</param>
    /// <param name="time">The clock the times are said against, or <see langword="null"/> for the system's.</param>
    public HomeViewModel(Localizer localizer, CharactersViewModel characters, Func<AssetLibrary?> library, Action<string> open, TimeProvider? time = null)
    {
        _localizer = localizer;
        _characters = characters;
        _library = library;
        _open = open;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Reads the characters and the assets again, and changes the lists where they differ.</summary>
    public void Refresh()
    {
        Notice?.Refresh();
        RefreshCharacters();
        RefreshRecents();
    }

    [RelayCommand]
    private void NewProfile()
    {
        ProfileRequested?.Invoke();
    }

    [RelayCommand]
    private async Task NewRoute()
    {
        if (RouteRequested is { } request)
            await request().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NewZone()
    {
        if (ZoneRequested is { } request)
            await request().ConfigureAwait(true);
    }

    [RelayCommand]
    private void Settings()
    {
        SettingsRequested?.Invoke();
    }

    [RelayCommand]
    private void ProjectFolder()
    {
        FolderRequested?.Invoke();
    }

    private void RefreshCharacters()
    {
        var wanted = new List<HomeCharacterViewModel>();

        foreach (var bot in _characters.Online)
        {
            wanted.Add(new HomeCharacterViewModel(_characters, bot.Bot.Key)
            {
                Name = bot.Name,
                Initials = bot.Initials,
                Detail = bot.Step.Length > 0 ? bot.Step : bot.CardActivity,
                Head = bot.Head,
                PortraitColor = bot.PortraitColor,
                StateBrush = OutlinerViewModel.StateOf(bot.PlayState),
            });
        }

        foreach (var line in _characters.Offline)
        {
            wanted.Add(new HomeCharacterViewModel(_characters, line.Key)
            {
                Name = line.Name,
                Initials = line.Initials,
                Detail = line.LastPlayed,
                Head = line.Head,
                PortraitColor = line.PortraitColor,
                IsDimmed = true,
            });
        }

        var signature = string.Join('\n', wanted.Select(row => string.Join('|', row.Key, row.Name, row.Detail, row.Head is not null, row.IsDimmed, (row.StateBrush as ISolidColorBrush)?.Color)));

        if (signature == _charactersSignature)
            return;

        _charactersSignature = signature;
        Characters.Clear();

        foreach (var row in wanted)
            Characters.Add(row);

        HasCharacters = Characters.Count > 0;
    }

    private void RefreshRecents()
    {
        // The host stopped or did not start: the assets last read stay.
        if (_library() is not { } library)
            return;

        if (!ReferenceEquals(library, _read) || library.Version != _version)
        {
            _read = library;
            _version = library.Version;
            _stamped = [.. library.Assets
                .Where(asset => !asset.IsGiven && library.FileOf(asset) is not null)
                .Select(asset => (Asset: asset, Changed: new DateTimeOffset(File.GetLastWriteTimeUtc(library.FileOf(asset)!), TimeSpan.Zero)))
                .OrderByDescending(stamped => stamped.Changed)
                .ThenBy(stamped => stamped.Asset.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(RecentCount)];
            _recentsSignature = null;
        }

        var now = _time.GetUtcNow();
        var wanted = _stamped.Select(stamped => new HomeRecentViewModel(stamped.Asset.Id, _open)
        {
            Name = stamped.Asset.Name,
            Kind = _localizer["AssetKind" + stamped.Asset.Kind],
            Icon = AssetKinds.IconOf(stamped.Asset.Kind),
            KindBrush = new ImmutableSolidColorBrush(AssetKinds.ColorOf(stamped.Asset.Kind)),
            KindTint = new ImmutableSolidColorBrush(AssetKinds.TintOf(stamped.Asset.Kind)),
            When = Ago.Of(_localizer, "Ago", stamped.Changed, now),
        }).ToList();
        var signature = string.Join('\n', wanted.Select(row => string.Join('|', row.Id, row.Name, row.Kind, row.When)));

        if (signature == _recentsSignature)
            return;

        _recentsSignature = signature;
        Recents.Clear();

        foreach (var row in wanted)
            Recents.Add(row);

        HasRecents = Recents.Count > 0;
    }
}
