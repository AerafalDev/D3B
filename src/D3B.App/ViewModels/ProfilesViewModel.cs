using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Settings;
using D3B.Data.Texts;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The profiles page: a column of profiles, the one being edited unfolded in place (D99, D101).</summary>
/// <remarks>
/// It reads the profiles and characters from memory each refresh, and makes its items again only once their books
/// changed: a profile being edited keeps its editor while the relay records what it sees.
/// </remarks>
internal sealed partial class ProfilesViewModel : ObservableObject
{
    private const int SummaryLength = 3;

    private readonly Dictionary<int, ProfileViewModel> _byId = [];
    private IServiceProvider? _services;
    private ActivityCatalog? _catalog;
    private int _profilesVersion = -1;
    private int _accountsVersion = -1;
    private CultureInfo? _culture;

    /// <summary>Gets the profiles, a new one first, then by name.</summary>
    public ObservableCollection<ProfileViewModel> Items { get; } = [];

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the activities a profile may run, named in the app's language.</summary>
    public IReadOnlyList<ActivityChoiceViewModel> Activities { get; private set; } = [];

    /// <summary>Gets the profiles, kept in the database.</summary>
    public ProfileBook? Profiles { get; private set; }

    /// <summary>Gets the game's values the settings name.</summary>
    public GameReferences? Game => _services?.GetService<GameReferences>();

    /// <summary>Gets what names those values.</summary>
    public GameNames Names { get; private set; }

    /// <summary>Gets how many profiles there are, and how many characters play one.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Gets whether there is no profile.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets whether the host keeps profiles: its database opened.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    private AccountBook? Accounts => _services?.GetService<AccountBook>();

    /// <summary>Initializes a new instance of the <see cref="ProfilesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    public ProfilesViewModel(Localizer localizer)
    {
        Localizer = localizer;
        Names = new GameNames(null, localizer);
    }

    /// <summary>Reads the profiles again, in the language chosen, once they changed.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _services is null)
        {
            _services = services;
            _catalog = services.GetService<ActivityCatalog>();
            Profiles = services.GetService<ProfileBook>();
            Names = new GameNames(services.GetService<GameTexts>(), Localizer);
            IsAvailable = Profiles is not null;
        }

        if (Profiles is null)
            return;

        var relabel = !Localizer.Culture.Equals(_culture);

        if (relabel)
        {
            _culture = Localizer.Culture;
            Activities = [.. (_catalog?.All ?? []).Select(definition => new ActivityChoiceViewModel(definition, Localizer.Culture))];
            OnPropertyChanged(nameof(Activities));
        }

        if (relabel || Profiles.Version != _profilesVersion || (Accounts?.Version ?? 0) != _accountsVersion)
            Sync(relabel);
    }

    /// <summary>Finds an activity the modules registered.</summary>
    /// <param name="id">Its id.</param>
    /// <returns>The activity, or <see langword="null"/> when none has the id.</returns>
    public ActivityDefinition? ActivityOf(string id)
    {
        return _catalog is not null && _catalog.TryGet(id, out var definition) ? definition : null;
    }

    /// <summary>Gets the characters that play a profile.</summary>
    /// <param name="id">The profile's id.</param>
    /// <returns>The characters.</returns>
    public IReadOnlyList<KnownCharacter> PlayersOf(int id)
    {
        return [.. (Accounts?.Characters ?? []).Where(character => character.ProfileId == id)];
    }

    /// <summary>Says a few of a profile's settings, those that show, at a glance.</summary>
    /// <param name="declared">The settings its activity declares.</param>
    /// <param name="settings">The profile's settings.</param>
    /// <returns>Three of them at most, joined.</returns>
    public string Summarize(ActivitySettings declared, object? settings)
    {
        if (settings is null)
            return "";

        var culture = Localizer.Culture;
        var parts = new List<string>();

        foreach (var setting in declared.All.Where(setting => declared.IsVisible(setting, settings)))
        {
            if (Describe(setting, settings) is { Length: > 0 } value)
                parts.Add(setting.Kind is SettingKind.Flag ? value : Localizer.Format("SummaryItem", setting.Name.In(culture), value));

            if (parts.Count == SummaryLength)
                break;
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Makes its editor the only one unfolded.</summary>
    /// <param name="item">The profile being edited.</param>
    public void BeginEditing(ProfileViewModel item)
    {
        foreach (var other in Items.Where(other => other != item && other.IsEditing).ToList())
            other.Cancel();
    }

    /// <summary>Drops a new profile, never saved.</summary>
    /// <param name="item">The profile.</param>
    public void Discard(ProfileViewModel item)
    {
        Items.Remove(item);
        IsEmpty = Items.Count == 0;
    }

    /// <summary>Keeps a profile saved, by its id.</summary>
    /// <param name="item">The profile.</param>
    public void Saved(ProfileViewModel item)
    {
        _byId[item.Id] = item;
        Sync(false);
    }

    /// <summary>Drops a profile deleted.</summary>
    /// <param name="item">The profile.</param>
    public void Deleted(ProfileViewModel item)
    {
        _byId.Remove(item.Id);
        Items.Remove(item);
        Sync(false);
    }

    private string Describe(SettingDefinition setting, object settings)
    {
        var culture = Localizer.Culture;
        var value = setting.GetValue(settings);

        switch (setting.Kind)
        {
            case SettingKind.Flag:
                return value is true ? setting.Name.In(culture) : "";

            case SettingKind.Number:
                return ((int)value!).ToString(culture);

            case SettingKind.Text:
                return (string)value!;

            case SettingKind.Choice:
                {
                    var names = setting.GetValues(settings).Select(choice => setting.Options.FirstOrDefault(option => option.Value.Equals(choice))?.Text.In(culture) ?? choice.ToString()!);

                    return Joined(setting.IsList ? [.. names] : [setting.Options.FirstOrDefault(option => option.Value.Equals(value))?.Text.In(culture) ?? ""]);
                }

            default:
                {
                    IReadOnlyList<object> ids = setting.IsList ? setting.GetValues(settings) : value is int id ? [id] : [];

                    return Joined([.. ids.Select(item => Names.Name(setting.Kind, Game?.Options(setting.Kind).FirstOrDefault(option => option.Id == (int)item) ?? new GameReference((int)item, 0, null)))]);
                }
        }
    }

    private string Joined(IReadOnlyList<string> names)
    {
        var shown = names.Where(name => name.Length > 0).Take(2).ToList();

        if (shown.Count == 0)
            return "";

        var rest = names.Count - shown.Count;

        return rest > 0 ? Localizer.Format("AndMore", string.Join(", ", shown), rest) : string.Join(", ", shown);
    }

    private void Sync(bool relabel)
    {
        var profiles = Profiles!.Profiles;
        var ids = profiles.Select(profile => profile.Id).ToHashSet();

        _profilesVersion = Profiles.Version;
        _accountsVersion = Accounts?.Version ?? 0;

        foreach (var gone in _byId.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            Items.Remove(_byId[gone]);
            _byId.Remove(gone);
        }

        foreach (var profile in profiles)
        {
            if (_byId.TryGetValue(profile.Id, out var item))
            {
                item.Update(profile);

                if (relabel)
                    item.Relabel();
            }
            else
            {
                _byId[profile.Id] = new ProfileViewModel(this, profile);
            }
        }

        foreach (var item in Items.Where(item => item.IsNew))
            item.Relabel();

        // A new profile not saved yet stays first; the others follow the book's order, by name.
        List<ProfileViewModel> order = [.. Items.Where(item => item.IsNew), .. profiles.Select(profile => _byId[profile.Id])];

        for (var index = 0; index < order.Count; index++)
        {
            var at = Items.IndexOf(order[index]);

            if (at < 0)
                Items.Insert(index, order[index]);
            else if (at != index)
                Items.Move(at, index);
        }

        IsEmpty = Items.Count == 0;
        Summary = Localizer.Format(
            "ProfilesSummary",
            Localizer.Plural("Profiles", profiles.Count),
            Localizer.Plural("Profiled", (Accounts?.Characters ?? []).Count(character => character.ProfileId is { } id && ids.Contains(id))));
    }

    [RelayCommand]
    private void New()
    {
        if (Items.FirstOrDefault(item => item.IsNew) is { } pending)
        {
            pending.Edit();

            return;
        }

        var item = new ProfileViewModel(this, null);

        Items.Insert(0, item);
        IsEmpty = false;
        item.Edit();
    }
}
