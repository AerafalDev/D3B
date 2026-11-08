using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Sequences;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Data.Texts;
using D3B.Storage.Accounts;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The profiles: each one's item, those whose editor is open, each in a tab of its own, and their dialogs (D99, D330).</summary>
/// <remarks>
/// <para>
/// It reads the profiles, assets of the project since 7b (D296, D300), and the characters from memory each refresh, and
/// makes its items again only once they changed: a profile being edited keeps its editor while the relay records what it
/// sees, and while another tab shows.
/// </para>
/// <para>
/// Until the studio's editor (D306), a profile shows as the settings of the activity its behavior plays whole
/// (<see cref="ProfileForms"/>), saved as the values it changes; the profiles given with the app are copied to be changed.
/// </para>
/// </remarks>
internal sealed partial class ProfilesViewModel : ObservableObject
{
    private const int CopyAttempts = 50;

    private readonly Dictionary<string, ProfileViewModel> _byId = new(StringComparer.Ordinal);
    private readonly List<ProfileViewModel> _open = [];
    private string _newFolder = "";
    private IServiceProvider? _services;
    private ActivityCatalog? _catalog;
    private NodeCatalog? _nodes;
    private int _profilesVersion = -1;
    private int _accountsVersion = -1;
    private CultureInfo? _culture;

    /// <summary>Gets the profiles, a new one first, then by name.</summary>
    public ObservableCollection<ProfileViewModel> Items { get; } = [];

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the window's dialogs.</summary>
    public Dialogs Dialogs { get; }

    /// <summary>Gets what the window says in its toasts.</summary>
    public Toasts Toasts { get; }

    /// <summary>Gets the activities a profile may run, named in the app's language.</summary>
    public IReadOnlyList<ActivityChoiceViewModel> Activities { get; private set; } = [];

    /// <summary>Gets the project, which holds the profiles and their behaviors.</summary>
    public AssetLibrary? Assets { get; private set; }

    /// <summary>Gets the game's values the settings name.</summary>
    public GameReferences? Game => _services?.GetService<GameReferences>();

    /// <summary>Gets what names those values.</summary>
    public GameNames Names { get; private set; }

    /// <summary>Gets the host's services, once it runs: an activity reckons what its settings lead to from them (D127).</summary>
    public IServiceProvider? Services => _services;

    /// <summary>Gets the profiles whose editor is open, each in a tab of its own (D330).</summary>
    public IReadOnlyList<ProfileViewModel> Open => _open;

    /// <summary>Gets or sets what opens a profile's tab once its editor opens.</summary>
    public Action<ProfileViewModel>? Opened { get; set; }

    /// <summary>Gets or sets what closes a profile's tab once its editor closes.</summary>
    public Action<ProfileViewModel>? Closed { get; set; }

    /// <summary>Gets or sets the lists of steps, which a profile plays and its page opens (D386).</summary>
    public SequencesViewModel? Sequences { get; set; }

    /// <summary>Gets whether an editor holds changes not saved.</summary>
    public bool IsAnyDirty => _open.Any(item => item.IsDirty);

    /// <summary>Gets the profile whose editor opened last, if any is open.</summary>
    [ObservableProperty]
    public partial ProfileViewModel? Editing { get; private set; }

    /// <summary>Gets whether there is no profile.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets whether the host keeps profiles: its database opened.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    private AccountBook? Accounts => _services?.GetService<AccountBook>();

    /// <summary>Initializes a new instance of the <see cref="ProfilesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="toasts">What the window says in its toasts.</param>
    public ProfilesViewModel(Localizer localizer, Dialogs dialogs, Toasts toasts)
    {
        Localizer = localizer;
        Dialogs = dialogs;
        Toasts = toasts;
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
            _nodes = services.GetService<NodeCatalog>();
            Assets = services.GetService<AssetLibrary>();
            Names = new GameNames(services.GetService<GameTexts>(), Localizer);
            IsAvailable = Assets is not null && _nodes is not null;
        }

        if (Assets is null || _nodes is null)
            return;

        var relabel = !Localizer.Culture.Equals(_culture);

        if (relabel)
        {
            _culture = Localizer.Culture;
            Activities = [.. (_catalog?.All ?? []).Where(definition => Assets.Find<BehaviorAsset>(GivenAssets.BehaviorOf(definition.Id)) is not null).Select(definition => new ActivityChoiceViewModel(definition, Localizer.Culture))];
            OnPropertyChanged(nameof(Activities));
        }

        if (relabel || Assets.Version != _profilesVersion || (Accounts?.Version ?? 0) != _accountsVersion)
            Sync(relabel);
    }

    /// <summary>Gets the behaviors of the project a profile may play besides the activities: the lists of steps, and what the canvas will make (D386).</summary>
    /// <returns>The behaviors, by name.</returns>
    public IReadOnlyList<BehaviorChoiceViewModel> Behaviors()
    {
        if (Assets is not { } assets)
            return [];

        var culture = Localizer.Culture;

        return
        [
            .. assets.OfType<BehaviorAsset>()
                .Where(behavior => !behavior.IsGiven)
                .OrderBy(behavior => behavior.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(behavior => new BehaviorChoiceViewModel(behavior.Id, behavior.Name, BehaviorSequences.Read(behavior) is { } list ? Localizer.Plural("SequenceStepsDetail", list.Steps.Length) : Localizer["BehaviorNotAList"])),
        ];
    }

    /// <summary>Opens a behavior's editor: the list of steps, in its tab.</summary>
    /// <param name="id">The behavior's id.</param>
    public void OpenSequence(string id)
    {
        Sequences?.OpenList(id);
    }

    /// <summary>Finds an activity the modules registered.</summary>
    /// <param name="id">Its id.</param>
    /// <returns>The activity, or <see langword="null"/> when none has the id.</returns>
    public ActivityDefinition? ActivityOf(string id)
    {
        return _catalog is not null && _catalog.TryGet(id, out var definition) ? definition : null;
    }

    /// <summary>Gets a profile as the settings of the activity its behavior plays whole.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The activity and the settings as JSON; none when its behavior is gone, or is more than one activity.</returns>
    public (ActivityDefinition? Activity, string? Settings) FormOf(ProfileAsset? profile)
    {
        if (profile is null || _nodes is null || Assets?.Find<BehaviorAsset>(profile.Behavior) is not { } behavior || ProfileForms.ActivityOf(behavior, _nodes) is not { } activity)
            return (null, null);

        return (activity, ProfileForms.SettingsOf(behavior, profile.Values));
    }

    /// <summary>Gets the behavior a profile of an activity plays: the one given with the app for it.</summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The behavior, or <see langword="null"/> when none is given for it.</returns>
    public BehaviorAsset? BehaviorOf(ActivityDefinition activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return Assets?.Find<BehaviorAsset>(GivenAssets.BehaviorOf(activity.Id));
    }

    /// <summary>Tells whether a name is another profile's, whatever its case.</summary>
    /// <param name="name">The name.</param>
    /// <param name="exceptId">The profile the name is for, which may keep its own.</param>
    /// <returns>Whether another profile has it.</returns>
    public bool IsTaken(string name, string? exceptId)
    {
        return Assets?.IsTaken(AssetKind.Profile, name, exceptId) == true;
    }

    /// <summary>Gets the characters that play a profile.</summary>
    /// <param name="id">The profile's id.</param>
    /// <returns>The characters.</returns>
    public IReadOnlyList<KnownCharacter> PlayersOf(string id)
    {
        return [.. (Accounts?.Characters ?? []).Where(character => character.Profile == id)];
    }

    /// <summary>Gets the characters that play a profile, with their levels and their map, as their bots last saw them.</summary>
    /// <param name="id">The profile's id.</param>
    /// <returns>The characters; those no bot plays yet with no level known.</returns>
    public IReadOnlyList<PreviewPlayer> LevelsOf(string id)
    {
        var bots = _services?.GetService<Bots>();

        return
        [
            .. PlayersOf(id).Select(character => bots is not null && bots.TryGet(character.Key, out var bot)
                ? new PreviewPlayer(character.Name, bot.State.Level ?? 0, bot.State.Jobs.Values.ToDictionary(job => job.JobId, job => job.Level), bot.State.MapId)
                : new PreviewPlayer(character.Name, 0, new Dictionary<int, int>(), null)),
        ];
    }

    /// <summary>Opens a profile's editor in its tab, beside the others open.</summary>
    /// <param name="item">The profile being edited.</param>
    public void BeginEditing(ProfileViewModel item)
    {
        if (!_open.Contains(item))
            _open.Add(item);

        Editing = item;
        Opened?.Invoke(item);
    }

    /// <summary>Closes a profile's tab, once its editor closed.</summary>
    /// <param name="item">The profile that was edited.</param>
    public void EndEditing(ProfileViewModel item)
    {
        if (!_open.Remove(item))
            return;

        Editing = _open.LastOrDefault();
        Closed?.Invoke(item);
    }

    /// <summary>Saves every profile edited whose settings hold: File's Save all, Ctrl S.</summary>
    public void SaveAll()
    {
        foreach (var item in _open.Where(item => item.IsDirty && item.SaveCommand.CanExecute(null)).ToList())
            item.SaveCommand.Execute(null);
    }

    /// <summary>Copies a profile given with the app into the project, under a name of its own, and opens the copy.</summary>
    /// <param name="item">The profile given.</param>
    /// <returns>A task that completes once the copy is saved and opened.</returns>
    public async Task CopyToProjectAsync(ProfileViewModel item)
    {
        if (Assets is not { } assets || assets.Find<ProfileAsset>(item.Id) is not { } given)
            return;

        try
        {
            var copy = await assets.SaveAsync(given with { Id = AssetLibrary.NewId(), Name = CopyName(item.Name), Title = null, Path = null }, CancellationToken.None);

            Sync(false);

            if (_byId.TryGetValue(copy.Id, out var opened))
                opened.Edit();

            Toasts.Show(new Toast(Localizer.Format("AssetCopied", copy.Name), Tone.Success));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Toasts.Show(new Toast(Localizer.Format("SaveFailed", exception.GetBaseException().Message), Tone.Danger));
        }
    }

    /// <summary>Finds a profile's item by its id.</summary>
    /// <param name="id">The profile's id.</param>
    /// <returns>The item, or <see langword="null"/>.</returns>
    public ProfileViewModel? Find(string id)
    {
        return _byId.GetValueOrDefault(id);
    }

    /// <summary>Opens the editor of a new profile, its name and activity chosen, its settings another's or the activity's defaults.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="activity">Its activity.</param>
    /// <param name="settings">The settings it starts from, a profile's, or <see langword="null"/> for the activity's defaults.</param>
    public void Create(string name, ActivityChoiceViewModel activity, string? settings)
    {
        new ProfileViewModel(this, null) { Folder = _newFolder }.EditNew(name, activity, settings);
    }

    /// <summary>Opens the editor of a new profile that plays a behavior of the project.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="behaviorId">The behavior's id.</param>
    public void CreateWithBehavior(string name, string behaviorId)
    {
        new ProfileViewModel(this, null) { Folder = _newFolder }.EditNewBehavior(name, behaviorId);
    }

    /// <summary>Makes a new, empty list of steps, opens its editor, and opens the editor of a new profile that plays it.</summary>
    /// <param name="name">The profile's name, which the list takes too when no list has it.</param>
    /// <returns>A task that completes once both are open.</returns>
    public async Task CreateWithNewListAsync(string name)
    {
        if (Sequences is not { } sequences)
            return;

        var folder = _newFolder;
        var free = Assets?.IsTaken(AssetKind.Behavior, name) == false;

        if (await sequences.MakeAsync(folder, free ? name : null).ConfigureAwait(true) is not { } list)
            return;

        _newFolder = folder;
        CreateWithBehavior(name, list.Id);
        sequences.OpenList(list.Id);
    }

    /// <summary>Asks the name of a copy of a profile, which then opens in the editor.</summary>
    /// <param name="item">The profile copied.</param>
    public void Duplicate(ProfileViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // The copy goes beside the profile copied, as the tree's own duplicate does.
        _newFolder = Path.GetDirectoryName(Assets?.Find(item.Id)?.Path ?? "") ?? "";
        Dialogs.Show(new NewProfileViewModel(this, item));
    }

    /// <summary>Gives a name no profile has, from a profile's own.</summary>
    /// <param name="name">The profile's name.</param>
    /// <returns>Its copy's name.</returns>
    public string CopyName(string name)
    {
        for (var attempt = 1; attempt <= CopyAttempts; attempt++)
        {
            var copy = attempt == 1 ? Localizer.Format("CopyOf", name) : Localizer.Format("CopyOfNumbered", name, attempt);

            if (!IsTaken(copy, null))
                return copy;
        }

        return name;
    }

    /// <summary>Keeps a profile saved, by its id.</summary>
    /// <param name="item">The profile.</param>
    public void Saved(ProfileViewModel item)
    {
        _byId[item.Id] = item;
        Sync(false);
        Toasts.Show(new Toast(Localizer.Format("ProfileSaved", item.Name), Tone.Success));
    }

    /// <summary>Drops a profile deleted.</summary>
    /// <param name="item">The profile.</param>
    public void Deleted(ProfileViewModel item)
    {
        _byId.Remove(item.Id);
        Items.Remove(item);
        EndEditing(item);
        Sync(false);
        Toasts.Show(new Toast(Localizer.Format("ProfileDeleted", item.Name)));
    }

    private void Sync(bool relabel)
    {
        var profiles = Assets!.OfType<ProfileAsset>();
        var ids = profiles.Select(profile => profile.Id).ToHashSet(StringComparer.Ordinal);

        _profilesVersion = Assets.Version;
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

        if (relabel)
        {
            foreach (var editing in _open.Where(item => item.IsNew))
                editing.Relabel();
        }

        // A new profile shows in its editor alone until it is saved; the others follow the project's order, given first, by name.
        List<ProfileViewModel> order = [.. profiles.Select(profile => _byId[profile.Id])];

        for (var index = 0; index < order.Count; index++)
        {
            var at = Items.IndexOf(order[index]);

            if (at < 0)
                Items.Insert(index, order[index]);
            else if (at != index)
                Items.Move(at, index);
        }

        IsEmpty = Items.Count == 0;
    }

    /// <summary>Asks the name and activity of a new profile, saved in a folder of the project.</summary>
    /// <param name="folder">The folder, relative to the project's; empty for its root.</param>
    public void NewIn(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (!IsAvailable)
            return;

        _newFolder = folder;
        Dialogs.Show(new NewProfileViewModel(this, null));
    }

    [RelayCommand]
    private void New()
    {
        _newFolder = "";
        Dialogs.Show(new NewProfileViewModel(this, null));
    }
}
