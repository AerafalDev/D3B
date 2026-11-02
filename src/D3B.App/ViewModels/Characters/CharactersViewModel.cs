using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Activities;
using D3B.Bot.Assets;
using D3B.Bot.Characters;
using D3B.Data;
using D3B.Storage.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Characters;

/// <summary>The characters' panel: a card per character online, those offline folded under their heading, a line each (D323).</summary>
/// <remarks>
/// <para>
/// A click chooses the character the run block drives and the inspector shows; a double click or Enter opens its tab; the
/// right click chooses it, then plays, pauses, stops, gives a profile, the start on connection, or forgets its account.
/// </para>
/// <para>
/// Offline are the characters D3B knows, the server having listed them, and the bots the host made whose client left: the
/// one played last first.
/// </para>
/// </remarks>
internal sealed partial class CharactersViewModel : ObservableObject
{
    private readonly Dictionary<CharacterKey, CharacterLineViewModel> _lines = [];
    private readonly Localizer _localizer;
    private readonly BotsViewModel _bots;
    private readonly CharacterAccounts _accounts;
    private readonly Action<CharacterKey> _choose;
    private readonly Action<CharacterKey> _open;
    private AccountBook? _book;
    private AssetLibrary? _assets;
    private ActivityCatalog? _activities;
    private GameIcons? _icons;
    private TimeProvider _time = TimeProvider.System;
    private int _profilesVersion = -1;

    /// <summary>Gets or sets the run block, which plays, pauses and stops the character chosen, as the menu asks.</summary>
    public Studio.RunBlockViewModel? RunBlock { get; set; }

    /// <summary>Gets the characters online, a card each, in the order the host made their bots.</summary>
    public ObservableCollection<BotViewModel> Online { get; } = [];

    /// <summary>Gets the characters offline, a line each, the one played last first.</summary>
    public ObservableCollection<CharacterLineViewModel> Offline { get; } = [];

    /// <summary>Gets the project's profiles, which the right click's menu gives.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProfileChoice> MyProfiles { get; private set; } = [];

    /// <summary>Gets the profiles given with D3B, which the right click's menu gives.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProfileChoice> GivenProfiles { get; private set; } = [];

    /// <summary>Gets the activities, each with its defaults, which the right click's menu gives for the session.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProfileChoice> ActivityProfiles { get; private set; } = [];

    /// <summary>Gets or sets the character chosen, whose card is outlined and whose menu the right click opens.</summary>
    [ObservableProperty]
    public partial CharacterKey? Chosen { get; set; }

    /// <summary>Gets whether the character chosen starts its profile on connection, which the menu checks.</summary>
    [ObservableProperty]
    public partial bool ChosenStartsOnConnection { get; private set; }

    /// <summary>Gets whether the character chosen has a profile, without which it never starts on connection.</summary>
    [ObservableProperty]
    public partial bool ChosenHasProfile { get; private set; }

    /// <summary>Gets whether the character chosen is online, so that the menu plays, pauses and stops it.</summary>
    [ObservableProperty]
    public partial bool ChosenIsOnline { get; private set; }

    /// <summary>Gets or sets whether the characters offline are unfolded.</summary>
    [ObservableProperty]
    public partial bool IsOfflineOpen { get; set; }

    /// <summary>Gets how many characters are offline, beside their heading.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOffline))]
    public partial int OfflineCount { get; private set; }

    /// <summary>Gets whether D3B knows no character yet.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets whether a character is offline.</summary>
    public bool HasOffline => OfflineCount > 0;

    /// <summary>Initializes a new instance of the <see cref="CharactersViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="bots">The bots, whose cards are those of the characters online.</param>
    /// <param name="accounts">What the menu changes of a character in the database.</param>
    /// <param name="choose">Chooses a character for the run block and the inspector.</param>
    /// <param name="open">Opens a character's tab.</param>
    public CharactersViewModel(Localizer localizer, BotsViewModel bots, CharacterAccounts accounts, Action<CharacterKey> choose, Action<CharacterKey> open)
    {
        _localizer = localizer;
        _bots = bots;
        _accounts = accounts;
        _choose = choose;
        _open = open;
    }

    /// <summary>Reads the characters again, after the bots' cards did.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>: the characters shown stay.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _book is null)
        {
            _book = services.GetService<AccountBook>();
            _assets = services.GetService<AssetLibrary>();
            _activities = services.GetService<ActivityCatalog>();
            _icons = GameIcons.Of(services.GetService<GameTables>());
            _time = services.GetService<TimeProvider>() ?? TimeProvider.System;
            _accounts.Book = _book;
        }

        foreach (var bot in _bots.Items)
            bot.Panel = this;

        Sync(Online, [.. _bots.Items.Where(bot => bot.IsConnected)]);
        RefreshOffline();
        RefreshProfiles();

        foreach (var bot in Online)
            bot.IsChosen = bot.Bot.Key == Chosen;

        foreach (var line in Offline)
            line.IsChosen = line.Key == Chosen;

        var known = Chosen is { } key ? _book?.Find(key) : null;

        ChosenHasProfile = known?.Profile is not null;
        ChosenStartsOnConnection = known?.StartsOnConnection ?? false;
        ChosenIsOnline = Online.Any(bot => bot.Bot.Key == Chosen);
        IsEmpty = Online.Count == 0 && Offline.Count == 0;
    }

    /// <summary>Chooses a character, as a click on its card does.</summary>
    /// <param name="key">The character.</param>
    public void Choose(CharacterKey key)
    {
        _choose(key);
    }

    /// <summary>Opens a character's tab, as a double click or Enter on its card does.</summary>
    /// <param name="key">The character.</param>
    public void Open(CharacterKey key)
    {
        _open(key);
    }

    /// <summary>Finds what the panel shows of a character: its card's bot, or its line.</summary>
    /// <param name="key">The character.</param>
    /// <returns>Its line, if it is offline and D3B knows it.</returns>
    public CharacterLineViewModel? LineOf(CharacterKey key)
    {
        return _lines.GetValueOrDefault(key);
    }

    private static void Sync<T>(ObservableCollection<T> shown, List<T> wanted)
    {
        if (shown.SequenceEqual(wanted))
            return;

        shown.Clear();

        foreach (var item in wanted)
            shown.Add(item);
    }

    [RelayCommand]
    private void ToggleOffline()
    {
        IsOfflineOpen = !IsOfflineOpen;
    }

    [RelayCommand]
    private void OpenChosen()
    {
        if (Chosen is { } key)
            _open(key);
    }

    [RelayCommand]
    private void Assign(ProfileChoice? choice)
    {
        if (Chosen is not { } key)
            return;

        var bot = _bots.Items.FirstOrDefault(item => item.Bot.Key == key);

        // An activity with its defaults is the session's choice alone: the character keeps its profile.
        if (choice?.ActivityId is { } activity)
        {
            bot?.PreferActivity(activity);

            return;
        }

        bot?.Prefer(choice?.Id);
        _ = _accounts.AssignAsync(key, choice?.Id);
    }

    [RelayCommand]
    private void ToggleStartsOnConnection()
    {
        if (Chosen is { } key && ChosenHasProfile)
            _ = _accounts.SetStartsOnConnectionAsync(key, !ChosenStartsOnConnection);
    }

    [RelayCommand]
    private void Forget()
    {
        if (Chosen is { } key)
            _accounts.AskForget(key);
    }

    private void RefreshOffline()
    {
        var now = _time.GetUtcNow();
        var bots = _bots.Items.ToDictionary(bot => bot.Bot.Key);
        var keys = (_book?.Characters.Select(character => character.Key) ?? [])
            .Concat(bots.Keys)
            .Distinct()
            .Where(key => !bots.TryGetValue(key, out var bot) || !bot.IsConnected)
            .OrderByDescending(key => _book?.Find(key)?.LastPlayed ?? DateTimeOffset.MinValue)
            .ThenBy(key => key.AccountId)
            .ThenBy(key => key.CharacterId)
            .ToList();

        foreach (var key in keys)
        {
            if (!_lines.TryGetValue(key, out var line))
                _lines[key] = line = new CharacterLineViewModel(this, key);

            line.Refresh(_localizer, _book?.Find(key), bots.GetValueOrDefault(key), _icons, now);
        }

        Sync(Offline, [.. keys.Select(key => _lines[key])]);
        OfflineCount = Offline.Count;
    }

    private void RefreshProfiles()
    {
        if ((_assets?.Version ?? 0) == _profilesVersion && MyProfiles.Count + GivenProfiles.Count > 0)
            return;

        _profilesVersion = _assets?.Version ?? 0;
        (MyProfiles, GivenProfiles) = ProfileChoices.Of(_localizer, _assets);
        ActivityProfiles = ProfileChoices.Activities(_localizer, _activities);
    }
}
