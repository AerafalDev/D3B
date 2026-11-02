using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.App.ViewModels.Characters;
using D3B.Bot.Activities;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors;
using D3B.Bot.Characters;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Storage.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Properties;

/// <summary>A character in the inspector: who it is, then Game, Variables and Account, each a section under a rule (D324).</summary>
/// <remarks>
/// Game holds its profile as an asset's field that opens it, the start on connection, and the planning and fight preferences
/// their sessions bring (7f, 7h); Variables, the character's own, each edited where it shows; Account, the account, its
/// subscription and forgetting it. A value changed from its default shows an arrow that resets it.
/// </remarks>
internal sealed partial class CharacterPropertiesViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly CharacterAccounts _accounts;
    private readonly Action<string> _openProfile;
    private readonly Action<CharacterKey> _openTab;
    private readonly Func<CharacterKey, BotViewModel?> _botOf;
    private readonly Func<CharacterKey, CharacterLineViewModel?> _lineOf;
    private AccountBook? _book;
    private AssetLibrary? _assets;
    private ActivityCatalog? _activities;
    private CharacterVariables? _variables;
    private GameTables? _tables;
    private GameTexts? _texts;
    private string _filter = "";
    private bool _reading;

    /// <summary>Gets the character shown.</summary>
    [ObservableProperty]
    public partial CharacterKey? Key { get; private set; }

    /// <summary>Gets its name.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets its breed, sex, level, as the header says them.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; private set; } = "";

    /// <summary>Gets its initials, its portrait without a head.</summary>
    [ObservableProperty]
    public partial string Initials { get; private set; } = "";

    /// <summary>Gets its class's head.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHead))]
    public partial IImage? Head { get; private set; }

    /// <summary>Gets its portrait's colour.</summary>
    [ObservableProperty]
    public partial IBrush PortraitColor { get; private set; } = BreedColors.Of(null);

    /// <summary>Gets where it stands, for its portrait's ring.</summary>
    [ObservableProperty]
    public partial PlayState PlayState { get; private set; } = PlayState.Offline;

    /// <summary>Gets the profile it plays, or none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfile))]
    [NotifyCanExecuteChangedFor(nameof(OpenProfileCommand))]
    public partial ProfileChoice? Profile { get; private set; }

    /// <summary>Gets the project's profiles, which its field's menu gives.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProfileChoice> MyProfiles { get; private set; } = [];

    /// <summary>Gets the profiles given with D3B, which its field's menu gives.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProfileChoice> GivenProfiles { get; private set; } = [];

    /// <summary>Gets the activities, each with its defaults, which its field's menu gives for the session.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProfileChoice> ActivityProfiles { get; private set; } = [];

    /// <summary>Gets or sets whether its profile starts once its client connects.</summary>
    [ObservableProperty]
    public partial bool StartsOnConnection { get; set; }

    /// <summary>Gets its variables, the character's own.</summary>
    public ObservableCollection<VariableRowViewModel> Variables { get; } = [];

    /// <summary>Gets its account, by nickname and tag.</summary>
    [ObservableProperty]
    public partial string Account { get; private set; } = "";

    /// <summary>Gets until when its account is subscribed, or that it is not.</summary>
    [ObservableProperty]
    public partial string Subscription { get; private set; } = "";

    /// <summary>Gets whether its account is subscribed now.</summary>
    [ObservableProperty]
    public partial bool IsSubscribed { get; private set; }

    /// <summary>Gets whether the search shows the profile's row.</summary>
    [ObservableProperty]
    public partial bool ShowsProfile { get; private set; } = true;

    /// <summary>Gets whether the search shows the start's row.</summary>
    [ObservableProperty]
    public partial bool ShowsStart { get; private set; } = true;

    /// <summary>Gets whether the search shows the planning's row.</summary>
    [ObservableProperty]
    public partial bool ShowsPlanning { get; private set; } = true;

    /// <summary>Gets whether the search shows the fight preferences' row.</summary>
    [ObservableProperty]
    public partial bool ShowsFight { get; private set; } = true;

    /// <summary>Gets whether the search shows the account's row.</summary>
    [ObservableProperty]
    public partial bool ShowsAccount { get; private set; } = true;

    /// <summary>Gets whether the search shows the subscription's row.</summary>
    [ObservableProperty]
    public partial bool ShowsSubscription { get; private set; } = true;

    /// <summary>Gets whether the search shows forgetting the account.</summary>
    [ObservableProperty]
    public partial bool ShowsForget { get; private set; } = true;

    /// <summary>Gets whether the search shows a row of Game.</summary>
    [ObservableProperty]
    public partial bool ShowsGame { get; private set; } = true;

    /// <summary>Gets whether the search shows Variables.</summary>
    [ObservableProperty]
    public partial bool ShowsVariables { get; private set; } = true;

    /// <summary>Gets whether the search shows a row of Account.</summary>
    [ObservableProperty]
    public partial bool ShowsAccountSection { get; private set; } = true;

    /// <summary>Gets whether the character has a profile, without which it never starts on connection.</summary>
    public bool HasProfile => Profile?.Id is not null;

    /// <summary>Gets whether the start on connection differs from its default, off.</summary>
    public bool IsStartChanged => StartsOnConnection;

    /// <summary>Gets whether the character has variables.</summary>
    public bool HasVariables => Variables.Count > 0;

    /// <summary>Gets whether the header shows its class's head.</summary>
    public bool HasHead => Head is not null;

    /// <summary>Initializes a new instance of the <see cref="CharacterPropertiesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="accounts">What the inspector changes of a character in the database.</param>
    /// <param name="botOf">Finds a character's bot, if the host made one.</param>
    /// <param name="lineOf">Finds a character's line offline, if D3B knows it.</param>
    /// <param name="openProfile">Opens a profile, by its id.</param>
    /// <param name="openTab">Opens a character's tab.</param>
    public CharacterPropertiesViewModel(Localizer localizer, CharacterAccounts accounts, Func<CharacterKey, BotViewModel?> botOf, Func<CharacterKey, CharacterLineViewModel?> lineOf, Action<string> openProfile, Action<CharacterKey> openTab)
    {
        _localizer = localizer;
        _accounts = accounts;
        _botOf = botOf;
        _lineOf = lineOf;
        _openProfile = openProfile;
        _openTab = openTab;
    }

    /// <summary>Shows a character.</summary>
    /// <param name="key">The character.</param>
    public void Show(CharacterKey key)
    {
        if (Key != key)
            Variables.Clear();

        Key = key;
    }

    /// <summary>Reads the character again, in the language chosen.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _book is null)
        {
            _book = services.GetService<AccountBook>();
            _assets = services.GetService<AssetLibrary>();
            _activities = services.GetService<ActivityCatalog>();
            _variables = services.GetService<CharacterVariables>();
            _tables = services.GetService<GameTables>();
            _texts = services.GetService<GameTexts>();
        }

        if (Key is not { } key)
            return;

        var known = _book?.Find(key);
        var bot = _botOf(key);
        var line = _lineOf(key);

        Name = bot?.Name ?? line?.Name ?? known?.Name ?? "";
        Initials = bot?.Initials ?? line?.Initials ?? AvatarInitials.Of(Name);
        Head = bot?.Head ?? line?.Head;
        PortraitColor = bot?.PortraitColor ?? line?.PortraitColor ?? BreedColors.Of(known?.BreedId);
        PlayState = bot?.PlayState ?? PlayState.Offline;
        Subtitle = SubtitleOf(known, bot);
        (MyProfiles, GivenProfiles) = ProfileChoices.Of(_localizer, _assets);
        ActivityProfiles = ProfileChoices.Activities(_localizer, _activities);
        Profile = bot?.SelectedActivity is { Profile: null, Definition: { } chosen } ? ProfileChoices.Activity(_localizer, chosen)
            : known?.Profile is { } id && _assets?.Find<ProfileAsset>(id) is { } profile ? ProfileChoices.Choice(_localizer, _assets, profile)
            : new ProfileChoice(null, _localizer["NoProfile"], "", false);
        _reading = true;
        StartsOnConnection = known?.StartsOnConnection ?? false;
        _reading = false;
        OnPropertyChanged(nameof(IsStartChanged));
        RefreshVariables(key);
        RefreshAccount(key);
        ApplyFilter(_filter);
    }

    /// <summary>Shows only the rows whose label or value holds a text, as the inspector's search asks.</summary>
    /// <param name="filter">The text, or empty for every row.</param>
    public void ApplyFilter(string filter)
    {
        _filter = filter.Trim();

        bool Has(params string[] texts)
        {
            return _filter.Length == 0 || texts.Any(text => text.Contains(_filter, StringComparison.CurrentCultureIgnoreCase));
        }

        ShowsProfile = Has(_localizer["PropertyProfile"], Profile?.Name ?? "");
        ShowsStart = Has(_localizer["PropertyStart"]);
        ShowsPlanning = Has(_localizer["PropertyPlanning"]);
        ShowsFight = Has(_localizer["PropertyFight"]);
        ShowsAccount = Has(_localizer["PropertyAccount"], Account);
        ShowsSubscription = Has(_localizer["PropertySubscription"], Subscription);
        ShowsForget = Has(_localizer["ForgetAccount"]);

        foreach (var variable in Variables)
            variable.IsShown = Has(variable.Name, variable.Text);

        ShowsGame = ShowsProfile || ShowsStart || ShowsPlanning || ShowsFight;
        ShowsVariables = _filter.Length == 0 || Variables.Any(variable => variable.IsShown);
        ShowsAccountSection = ShowsAccount || ShowsSubscription || ShowsForget;
    }

    partial void OnStartsOnConnectionChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStartChanged));

        if (!_reading && Key is { } key)
            _ = _accounts.SetStartsOnConnectionAsync(key, value);
    }

    [RelayCommand]
    private void Assign(ProfileChoice? choice)
    {
        if (Key is not { } key)
            return;

        // An activity with its defaults is the session's choice alone: the character keeps its profile.
        if (choice?.ActivityId is { } activity)
        {
            _botOf(key)?.PreferActivity(activity);

            return;
        }

        _botOf(key)?.Prefer(choice?.Id);
        _ = _accounts.AssignAsync(key, choice?.Id);
    }

    [RelayCommand(CanExecute = nameof(HasProfile))]
    private void OpenProfile()
    {
        if (Profile?.Id is { } id)
            _openProfile(id);
    }

    [RelayCommand]
    private void ResetStart()
    {
        StartsOnConnection = false;
    }

    [RelayCommand]
    private void OpenTab()
    {
        if (Key is { } key)
            _openTab(key);
    }

    [RelayCommand]
    private void Forget()
    {
        if (Key is { } key)
            _accounts.AskForget(key);
    }

    private string SubtitleOf(KnownCharacter? known, BotViewModel? bot)
    {
        var parts = new List<string>();

        if (known?.BreedId is { } breedId && _tables is not null && _texts is not null && _tables.Breeds.TryGetBreed(breedId, out var breed) && _texts.TryGet(_localizer.GameLanguage, breed.NameId, out var breedName))
            parts.Add(known.IsFemale is { } female ? $"{breedName} {(female ? "♀" : "♂")}" : breedName);

        if ((bot?.Bot.State.Level ?? known?.Level) is { } level)
            parts.Add(_localizer.Format("LevelLower", level));

        return string.Join(" · ", parts);
    }

    private void RefreshVariables(CharacterKey key)
    {
        var values = _variables?.All().Where(variable => variable.Character == key).OrderBy(variable => variable.Name, StringComparer.Ordinal).ToList() ?? [];

        if (!values.Select(variable => variable.Name).SequenceEqual(Variables.Select(row => row.Name)))
        {
            Variables.Clear();

            foreach (var variable in values)
                Variables.Add(new VariableRowViewModel(_variables!, key, variable.Name, variable.Value));

            OnPropertyChanged(nameof(HasVariables));

            return;
        }

        for (var i = 0; i < values.Count; i++)
            Variables[i].Read(values[i].Value);
    }

    private void RefreshAccount(CharacterKey key)
    {
        var account = _book?.Accounts.FirstOrDefault(known => known.Id == key.AccountId);
        var now = DateTimeOffset.UtcNow;

        Account = account is null ? "" : account.Tag.Length > 0 ? $"{account.Nickname}#{account.Tag}" : account.Nickname;
        IsSubscribed = account?.SubscriptionEnd > now;
        Subscription = IsSubscribed ? _localizer.Format("SubscribedUntil", account!.SubscriptionEnd!.Value.ToLocalTime().DateTime) : _localizer["NotSubscribed"];
    }
}
