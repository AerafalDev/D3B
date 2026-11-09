using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.Updates;
using D3B.App.ViewModels.AppSettings;
using D3B.App.ViewModels.Assets;
using D3B.App.ViewModels.Bottom;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Home;
using D3B.App.ViewModels.Inspector;
using D3B.App.ViewModels.Logs;
using D3B.App.ViewModels.Properties;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Zones;
using D3B.App.ViewModels.Sequences;
using D3B.App.ViewModels.Statistics;
using D3B.App.ViewModels.Studio;
using D3B.App.ViewModels.World;
using D3B.Bot.Characters;
using D3B.Bot.Pages;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Studio.Assets;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The studio's window: where the bots' host stands, its docks and editor, the run block, the search, its dialogs and toasts (D14, D91, D320 to D322, D328).</summary>
/// <remarks>
/// The characters' panel, the inspector and a character's tab are the studio's own since 7c3 (D323 to D325). Until 7c4
/// draws them again, the journal, the protocol and the statistics hold the bottom panel, and the profiles, the settings and
/// the modules' pages are tabs of the editor (D333).
/// </remarks>
internal sealed partial class MainViewModel : ObservableObject
{
    private const string HomeTab = "home";
    private const string ProfileTab = "profile:";
    private const string WorldTab = "world";
    private const string SequenceTab = "sequence:";
    private const string SettingsTab = "settings";
    private const string CharacterTab = "character:";

    private readonly HostRunner _host;
    private readonly Localizer _localizer;
    private readonly AppPreferences? _preferences;
    private readonly Action _close;
    private readonly Dictionary<CharacterKey, CharacterTabViewModel> _characterTabs = [];
    private readonly CharacterAccounts _characterAccounts;
    private bool _pagesRead;
    private IUndoable? _followed;
    private List<SearchItem> _maps = [];
    private System.Globalization.CultureInfo? _mapsCulture;

    /// <summary>Gets the dialog the window shows over the studio.</summary>
    public Dialogs Dialogs { get; } = new();

    /// <summary>Gets what the window says in its toasts.</summary>
    public Toasts Toasts { get; } = new();

    /// <summary>Gets the docks' layout.</summary>
    public StudioLayout Layout { get; }

    /// <summary>Gets the editor's tabs.</summary>
    public EditorTabs Editor { get; } = new();

    /// <summary>Gets what is selected in the world, which the viewport, the Outliner and the Details panel share (D397).</summary>
    public WorldSelection Selection { get; } = new();

    /// <summary>Gets the home page, the first tab: quick actions, the characters and the assets changed last (D406).</summary>
    public HomeViewModel Home { get; }

    /// <summary>Gets the world's viewport, shown once a route or a zone is selected (D397, D406).</summary>
    public WorldViewportViewModel World { get; }

    /// <summary>Gets the Outliner: what the world holds, each with an eye (D397).</summary>
    public OutlinerViewModel Outliner { get; }

    /// <summary>Gets the title row's run block.</summary>
    public RunBlockViewModel RunBlock { get; }

    /// <summary>Gets the search, Ctrl K.</summary>
    public SearchViewModel Search { get; }

    /// <summary>Gets the bots, each character online's card.</summary>
    public BotsViewModel Bots { get; }

    /// <summary>Gets the characters' panel (D323).</summary>
    public CharactersViewModel Characters { get; }

    /// <summary>Gets the inspector (D324).</summary>
    public PropertiesViewModel Properties { get; }

    /// <summary>Gets the profiles, each edited in a tab of its own (D330).</summary>
    public ProfilesViewModel Profiles { get; }

    /// <summary>Gets the routes, each edited on the world's map in a tab of its own (D371).</summary>
    public RoutesViewModel Routes { get; }

    /// <summary>Gets the zones, whose editors open in tabs of their own (D394).</summary>
    public ZonesViewModel Zones { get; }

    /// <summary>Gets the behaviors that are lists of steps, whose editors are open (D386).</summary>
    public SequencesViewModel Sequences { get; }

    /// <summary>Gets the assets' panel (D326).</summary>
    public AssetsViewModel Assets { get; }

    /// <summary>Gets the bottom panel (D327).</summary>
    public BottomPanelViewModel Bottom { get; }

    /// <summary>Gets the settings, a tab of the editor (D329).</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Gets the app's version and its update, in the status bar and the settings (D173).</summary>
    public UpdatesViewModel Updates { get; }

    /// <summary>Gets the pages the host's modules registered, each opened in a tab of the editor.</summary>
    public ObservableCollection<ModulePageViewModel> Pages { get; } = [];

    /// <summary>Gets or sets what opens a folder in the system's file manager, as the window can.</summary>
    public Action<string>? OpenFolder { get; set; }

    /// <summary>Gets or sets what puts a text in the clipboard, as the window can.</summary>
    public Action<string>? CopyText { get; set; }

    /// <summary>Gets or sets what opens a file or a link with the system's own application, as the window can.</summary>
    public Action<string>? OpenFile { get; set; }

    /// <summary>Gets or sets what shows a file, chosen, in the system's file manager, as the window can.</summary>
    public Action<string>? RevealFile { get; set; }

    /// <summary>Gets or sets what puts the keyboard in the assets' search, as the window can: Ctrl Space.</summary>
    public Action? FocusAssets { get; set; }

    /// <summary>Gets whether any module registered a page.</summary>
    [ObservableProperty]
    public partial bool HasPages { get; private set; }

    /// <summary>Gets where the host stands, in a word.</summary>
    [ObservableProperty]
    public partial string HostTitle { get; private set; } = "";

    /// <summary>Gets what the host runs for, or what it does now.</summary>
    [ObservableProperty]
    public partial string HostDetail { get; private set; } = "";

    /// <summary>Gets the tone of where the host stands.</summary>
    [ObservableProperty]
    public partial Tone HostTone { get; private set; }

    /// <summary>Gets whether the host gets ready.</summary>
    [ObservableProperty]
    public partial bool IsStarting { get; private set; }

    /// <summary>Gets whether the host runs: its bots are shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRunning))]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets whether the host stops, or stopped.</summary>
    [ObservableProperty]
    public partial bool IsStopping { get; private set; }

    /// <summary>Gets whether the host did not start.</summary>
    [ObservableProperty]
    public partial bool IsFailed { get; private set; }

    /// <summary>Gets why the host did not start, and what to do about it, once it failed.</summary>
    [ObservableProperty]
    public partial string? HostFailure { get; private set; }

    /// <summary>Gets whether the installed app did not start for a client build it does not ship, which a newer version does (D172).</summary>
    [ObservableProperty]
    public partial bool IsMissingBuild { get; private set; }

    /// <summary>Gets what the window says of the client build the installed app does not ship.</summary>
    [ObservableProperty]
    public partial string MissingBuildText { get; private set; } = "";

    /// <summary>Gets the client build the host runs for, or the one the app ships, in the status bar.</summary>
    [ObservableProperty]
    public partial string ClientText { get; private set; } = "";

    /// <summary>Gets how many characters are connected, beside the characters' tab.</summary>
    [ObservableProperty]
    public partial string OnlineText { get; private set; } = "";

    /// <summary>Gets whether everything is saved: no profile's tab with changes not saved.</summary>
    [ObservableProperty]
    public partial bool IsSaved { get; private set; } = true;

    /// <summary>Gets whether the host does not run, which the panels that read it say.</summary>
    public bool IsNotRunning => !IsRunning;

    /// <summary>Gets the app's version and the client build it ships, under the welcome's logo.</summary>
    public string VersionText => _localizer.Format("HomeVersion", Updates.Version, Updates.ClientVersion);

    // The world's tab undoes what the route or the zone selected in it does: its map is theirs (D397).
    private IUndoable? Undoable => Editor.Selected?.Content switch
    {
        WorldViewportViewModel world => (IUndoable?)world.Route ?? world.Zone,
        IUndoable undoable => undoable,
        _ => null,
    };

    private bool CanUndo => Undoable?.CanUndo == true;

    private bool CanRedo => Undoable?.CanRedo == true;

    /// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
    /// <param name="host">The bots' host in the app's process.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="preferences">What the app remembers of its own, its language, layout and the host's settings, or <see langword="null"/> to remember nothing.</param>
    /// <param name="updates">The app's updates, or <see langword="null"/> for an app that never updates.</param>
    /// <param name="close">Closes the app, as File's Quit and restarting it to install its update do.</param>
    public MainViewModel(HostRunner host, Localizer localizer, AppPreferences? preferences = null, AppUpdates? updates = null, Action? close = null)
    {
        _host = host;
        _localizer = localizer;
        _preferences = preferences;
        _close = close ?? (() => { });

        var choice = new BotChoice();

        Layout = new StudioLayout(preferences);
        _characterAccounts = new CharacterAccounts(localizer, Dialogs, Toasts);
        Bots = new BotsViewModel(localizer, ShowJournalOf, OpenCharacter, Toasts);
        Characters = new CharactersViewModel(localizer, Bots, _characterAccounts, key => RunBlock!.Choose(key), OpenCharacter);
        Routes = new RoutesViewModel(localizer, Dialogs, Toasts) { Opened = ShowRoute, Closed = item => Unselect(item.Id) };
        Zones = new ZonesViewModel(localizer, Dialogs, Toasts, () => Routes.Services) { Opened = ShowZone, Closed = item => Unselect(item.Id) };
        Sequences = new SequencesViewModel(localizer, Dialogs, Toasts) { Opened = OpenSequenceTab, Closed = CloseSequenceTab };
        Properties = new PropertiesViewModel(
            new CharacterPropertiesViewModel(localizer, _characterAccounts, BotOf, Characters.LineOf, OpenProfile, OpenCharacter),
            new AssetPropertiesViewModel(localizer) { RouteOf = Routes.Find, ZoneOf = Zones.Find });
        Profiles = new ProfilesViewModel(localizer, Dialogs, Toasts) { Opened = OpenProfileTab, Closed = CloseProfileTab, Sequences = Sequences, WorldServices = () => Routes.Services, EditZone = EditZoneInWorld };
        Assets = new AssetsViewModel(localizer, Dialogs, Toasts, Profiles, preferences?.FoldedAssets)
        {
            Routes = Routes,
            Zones = Zones,
            Sequences = Sequences,
            OpenAssetId = () => Editor.Selected?.Content switch { ProfileViewModel { IsNew: false } open => open.Id, WorldViewportViewModel => Selection.Kind is WorldSelectionKind.Route or WorldSelectionKind.Zone ? Selection.AssetId : null, SequenceEditorViewModel sequence => sequence.Id, _ => null },
            FoldsChanged = folded => _preferences?.RememberFoldedAssets(folded),
            CopyText = text => CopyText?.Invoke(text),
            Choose = ChooseAsset,
            Open = OpenAsset,
            ShowUsages = ShowUsages,
            Reveal = path => RevealFile?.Invoke(path),
            IsUnsaved = id => Profiles.Find(id)?.IsDirty == true || Routes.Find(id)?.IsDirty == true || Zones.Find(id)?.IsDirty == true || Sequences.Find(id)?.IsDirty == true,
        };
        Properties.Asset.Rename = Assets.RenameAssetAsync;
        Bottom = new BottomPanelViewModel(Layout, () => _host.Services, new LogsViewModel(localizer, choice, Toasts), new InspectorViewModel(localizer, choice), new StatisticsViewModel(localizer));
        Bottom.HostFailure = () => IsFailed ? HostFailure : null;
        Bottom.Problems.OpenFolder = folder => OpenFolder?.Invoke(folder);
        Bottom.Problems.ChooseCharacter = key => RunBlock!.Choose(key);
        Updates = new UpdatesViewModel(localizer, updates, Dialogs, () => IsRunning && Bots.HasItems, _close);
        Settings = new SettingsViewModel(localizer, preferences, UseLanguage, Toasts, Updates)
        {
            ResetLayout = Layout.Reset,
            OpenFolder = path => OpenFolder?.Invoke(path),
            OpenFile = path => OpenFile?.Invoke(path),
        };
        RunBlock = new RunBlockViewModel(Bots, localizer) { Characters = Characters, Chose = Chose };
        Characters.RunBlock = RunBlock;
        Search = new SearchViewModel(localizer, SearchItems);
        Bots.Choose = RunBlock.Choose;
        ClientText = localizer.Format("StatusClient", Updates.ClientVersion);
        World = new WorldViewportViewModel(localizer, Routes, Zones, Selection, () => Bots.Items.Select(bot => new WorldCharacterSnapshot(bot.Bot.Key, bot.Name, bot.Initials, bot.PortraitColor, bot.Bot.State.MapId, bot.IsPlaying || bot.IsFighting)), () => RunBlock.IsPlaying ? RunBlock.Name : null);
        Outliner = new OutlinerViewModel(localizer, Selection, World, Characters, Routes, Zones) { Show = ShowWorld };
        Home = new HomeViewModel(localizer, Characters, () => _host.Services?.GetService<AssetLibrary>(), OpenAsset)
        {
            ProfileRequested = NewProfile,
            RouteRequested = () => Routes.CreateAsync(""),
            ZoneRequested = () => Zones.CreateAsync(""),
            SettingsRequested = ShowSettings,
            FolderRequested = OpenProjectFolder,
        };
        Selection.Changed += OnSelectionChanged;
        World.PropertyChanged += (_, e) => FollowEditor(e.PropertyName);
        Editor.PropertyChanged += (_, e) => FollowEditor(e.PropertyName);
        ShowHome();
    }

    private static IEnumerable<(string Key, SettingsSection Section)> SettingsFound()
    {
        yield return ("SettingDisplay", SettingsSection.Bots);
        yield return ("SettingDelays", SettingsSection.Bots);
        yield return ("SettingCaptures", SettingsSection.Network);
        yield return ("SettingLanguage", SettingsSection.Appearance);
        yield return ("SettingFiles", SettingsSection.Files);
        yield return ("SettingUpdate", SettingsSection.About);
    }

    /// <summary>Reads the host and its bots again, in the language chosen.</summary>
    public void Refresh()
    {
        var status = _host.Status;
        var services = _host.Services;

        IsStarting = status is HostStatus.Starting;
        IsRunning = status is HostStatus.Running;
        IsStopping = status is HostStatus.Stopping or HostStatus.Stopped;
        IsFailed = status is HostStatus.Failed;
        HostFailure = IsFailed ? _host.Failure : null;
        Updates.Refresh();

        // Run from its code, the app says what the maintainer does: extract and map the build; installed, that it updates.
        IsMissingBuild = IsFailed && _host.MissingBuild is not null && Updates.IsInstalled;
        MissingBuildText = IsMissingBuild ? _localizer.Format("MissingBuildText", _host.MissingBuild, Updates.Version, Updates.ClientVersion) : "";
        (HostTitle, HostTone) = status switch
        {
            HostStatus.Starting => (_localizer["HostStatusStarting"], Tone.Info),
            HostStatus.Running => (_localizer["HostStatusRunning"], Tone.Success),
            HostStatus.Failed => (_localizer["HostStatusFailed"], Tone.Danger),
            HostStatus.Stopping => (_localizer["HostStatusStopping"], Tone.Warning),
            _ => (_localizer["HostStatusStopped"], Tone.Neutral),
        };

        var build = services?.GetService<ClientBuild>()?.Version;

        HostDetail = status switch
        {
            HostStatus.Starting => _localizer["HostPreparing"],
            HostStatus.Running => _localizer.Format("HostClient", build ?? "?"),
            HostStatus.Failed => _localizer["HostFailedDetail"],
            _ => "",
        };
        ClientText = _localizer.Format("StatusClient", build ?? Updates.ClientVersion);

        // The profiles first: the bots' cards offer them, and the characters name them.
        Profiles.Refresh(services);
        Routes.Refresh(services);
        Zones.Refresh();
        World.Refresh();
        Profiles.RefreshMaps();
        Sequences.Refresh(services);
        Bots.Refresh(services);
        Characters.Refresh(services);
        RunBlock.Refresh();
        Outliner.Refresh();
        Home.Refresh();
        Properties.Refresh(services);
        OnlineText = _localizer.Format("CharactersOnline", RunBlock.Online.Count);
        IsSaved = !Profiles.IsAnyDirty && !Routes.IsAnyDirty && !Zones.IsAnyDirty && !Sequences.IsAnyDirty;
        Assets.Refresh(services);

        // Only what shows reads what came since: the rest reads it all once shown again.
        foreach (var (key, tab) in _characterTabs)
        {
            if (Editor.IsShown(CharacterTab + key))
                tab.Refresh(services);
        }

        // A character's tab is named by its character and shows its head, both of which the server may say late.
        Editor.Refresh();

        Bottom.Refresh(services);

        // Always: the bots' delays the settings show are read once the host runs, whichever tab shows.
        Settings.Refresh(services);
        RefreshPages(services);
    }

    /// <summary>Replaces the module's page whose own code threw what nothing else caught on the UI's thread.</summary>
    /// <param name="exception">What was thrown.</param>
    /// <returns>Whether a page's code threw it, and the page now shows it instead: else the app's own fault, not handled.</returns>
    public bool FailPageThatThrew(Exception exception)
    {
        return Pages.Any(page => page.TryFailWith(exception));
    }

    /// <summary>Shows the Sequencer of the route selected, in the bottom panel.</summary>
    public void ShowSequencer()
    {
        if (Bottom.HasSequencer)
            Bottom.Show(BottomTab.Sequencer);
    }

    /// <summary>Shows the bottom panel, or closes it: Ctrl J.</summary>
    [RelayCommand]
    public void ToggleBottom()
    {
        Bottom.Toggle();
    }

    /// <summary>Opens the assets, the keyboard in their search: Ctrl Space (D321).</summary>
    [RelayCommand]
    public void GoToAssets()
    {
        Layout.Show(StudioPanel.Assets, true);
        FocusAssets?.Invoke();
    }

    /// <summary>Shows the world's tab: opened when a route or a zone is selected, not with the studio, and closed like any tab (D397, D406).</summary>
    [RelayCommand]
    public void ShowWorld()
    {
        Editor.Open(WorldTab, () => new EditorTab(WorldTab, () => _localizer["TabWorld"], "General.Language", World, needsHost: true));
    }

    /// <summary>Opens a zone for painting: the world's tab shows, the zone is selected and the Zone tool chosen, as the map beside a profile offers (D415).</summary>
    /// <param name="id">The zone's id.</param>
    public void EditZoneInWorld(string id)
    {
        ShowWorld();
        Selection.SelectZone(id);
        World.SetTool(WorldTool.Zone);
    }

    /// <summary>Shows the home page, the first tab, opened with the studio; it closes like the others and the Window menu opens it again (D406, D412).</summary>
    [RelayCommand]
    public void ShowHome()
    {
        Editor.Open(HomeTab, () => new EditorTab(HomeTab, () => _localizer["Home"], null, this, needsHost: false));
    }

    /// <summary>Opens a character's tab: its map live and what it does (D325).</summary>
    /// <param name="key">The character.</param>
    public void OpenCharacter(CharacterKey key)
    {
        var tab = TabOf(key);

        // Read first: the tab is named by its character, and its head drawn, as it opens.
        tab.Refresh(_host.Services);
        Editor.Open(CharacterTab + key, () => new EditorTab(CharacterTab + key, () => tab.Name, "General.User", tab, needsHost: true) { Picture = () => BotOf(key)?.Head ?? Characters.LineOf(key)?.Head });
        Editor.Refresh();
    }

    /// <summary>Opens a profile in its tab, the inspector showing it (D330).</summary>
    /// <param name="id">The profile's id.</param>
    public void OpenProfile(string id)
    {
        ChooseAsset(id);
        Profiles.Find(id)?.Edit();
    }

    /// <summary>Opens a route on the world's map in its tab, the inspector showing it (D371).</summary>
    /// <param name="id">The route's id.</param>
    public void OpenRoute(string id)
    {
        Routes.OpenRoute(id);
    }

    /// <summary>Opens an asset in its tab: a profile or a route today, the other kinds once their editors come (D306).</summary>
    /// <param name="id">The asset's id.</param>
    public void OpenAsset(string id)
    {
        if (Profiles.Find(id) is not null)
            OpenProfile(id);
        else if (_host.Services?.GetService<AssetLibrary>()?.Find(id) is RouteAsset)
            OpenRoute(id);
        else if (_host.Services?.GetService<AssetLibrary>()?.Find(id) is ZoneAsset)
            OpenZone(id);
        else if (_host.Services?.GetService<AssetLibrary>()?.Find(id) is BehaviorAsset)
            OpenSequence(id);
        else
            ChooseAsset(id);
    }

    /// <summary>Opens a zone on the world's map in its tab, the inspector showing it (D394).</summary>
    /// <param name="id">The zone's id.</param>
    public void OpenZone(string id)
    {
        Zones.OpenZone(id);
    }

    /// <summary>Opens a behavior that is a list of steps in its tab (D386).</summary>
    /// <param name="id">The behavior's id.</param>
    public void OpenSequence(string id)
    {
        if (Sequences.OpenList(id) is not null)
            ChooseAsset(id);
    }

    /// <summary>Shows the settings' tab: the title's gear, Ctrl , (D329).</summary>
    [RelayCommand]
    public void ShowSettings()
    {
        ShowSettings(null);
    }

    /// <summary>Shows the settings' tab on a section, as a setting the search found asks.</summary>
    /// <param name="section">The section, or <see langword="null"/> for the one shown last.</param>
    public void ShowSettings(SettingsSection? section)
    {
        if (section is { } shown)
            Settings.Show(shown);

        Editor.Open(SettingsTab, () => new EditorTab(SettingsTab, () => _localizer["Settings"], "General.Settings", Settings, needsHost: false));
        Settings.Refresh(_host.Services);
    }

    /// <summary>Shows a module's page in a tab.</summary>
    /// <param name="page">The page.</param>
    [RelayCommand]
    public void ShowPage(ModulePageViewModel? page)
    {
        if (page is null)
            return;

        Editor.Open(page.Route, () => new EditorTab(page.Route, () => page.Title, "Toolwindows.Services", page, needsHost: true));
        RefreshPages(_host.Services);
    }

    /// <summary>Starts a new profile: File's New profile, Ctrl N.</summary>
    [RelayCommand]
    public void NewProfile()
    {
        if (IsRunning)
            Profiles.NewCommand.Execute(null);
    }

    /// <summary>Saves what is edited: File's Save all, Ctrl S.</summary>
    [RelayCommand]
    public void SaveAll()
    {
        Profiles.SaveAll();
        Routes.SaveAll();
        Zones.SaveAll();
        Sequences.SaveAll();
    }

    /// <summary>Takes back the last change of the editor shown: Edit's Undo, Ctrl Z.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        Undoable?.Undo();
    }

    /// <summary>Does again the change taken back in the editor shown: Edit's Redo, Ctrl Y.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        Undoable?.Redo();
    }

    /// <summary>Opens the project's folder in the system's file manager.</summary>
    [RelayCommand]
    public void OpenProjectFolder()
    {
        if (_host.Services?.GetService<AssetLibrary>()?.Folder is { } folder)
            OpenFolder?.Invoke(folder);
    }

    /// <summary>Lists the shortcuts: the search on its commands, each with its keys.</summary>
    [RelayCommand]
    public void ShowShortcuts()
    {
        Search.Open(SearchScope.Commands);
    }

    /// <summary>Closes the app: File's Quit.</summary>
    [RelayCommand]
    public void Quit()
    {
        _close();
    }

    /// <summary>Says what the editor's tabs and the search are named, in the language chosen.</summary>
    private void Retell()
    {
        Routes.Retell();
        Zones.Retell();
        Sequences.Retell();
        World.Relabel();
        Editor.Refresh();
        OnPropertyChanged(nameof(VersionText));
    }

    private void RefreshPages(IServiceProvider? services)
    {
        // The catalogue is the host's: read once the host runs, and kept after it stops, as the bots shown are.
        if (!_pagesRead && IsRunning && services is not null)
        {
            foreach (var definition in services.GetService<PageCatalog>()?.All ?? [])
                Pages.Add(new ModulePageViewModel(definition, services, _localizer, ShowPage));

            _pagesRead = true;
            HasPages = Pages.Count > 0;
        }

        foreach (var page in Pages)
        {
            page.IsSelected = Editor.IsShown(page.Route);
            page.Refresh();
        }
    }

    private IEnumerable<SearchItem> SearchItems()
    {
        foreach (var asset in _host.Services?.GetService<AssetLibrary>()?.Assets ?? [])
        {
            var folder = asset.IsGiven ? _localizer["GivenAssets"] : Path.GetDirectoryName(asset.Path) is { Length: > 0 } directory ? directory.Replace(Path.DirectorySeparatorChar, '/') : _localizer["ProjectRoot"];

            yield return new SearchItem(SearchScope.Assets, "asset:" + asset.Id, asset.Title?.In(_localizer.Culture) ?? asset.Name, $"{_localizer["AssetKind" + asset.Kind]} · {folder}", AssetKinds.IconOf(asset.Kind), "", "", () => OpenAsset(asset.Id)) { Color = new Avalonia.Media.SolidColorBrush(AssetKinds.ColorOf(asset.Kind)) };
        }

        foreach (var bot in Characters.Online)
            yield return new SearchItem(SearchScope.Characters, "character:" + bot.Bot.Key, bot.Name, bot.Level, null, bot.Initials, "", () => ChooseAndOpen(bot.Bot.Key)) { Picture = bot.Head, Color = bot.PortraitColor };

        foreach (var line in Characters.Offline)
            yield return new SearchItem(SearchScope.Characters, "character:" + line.Key, line.Name, _localizer["Disconnected"], null, line.Initials, "", () => ChooseAndOpen(line.Key)) { Picture = line.Head, Color = line.PortraitColor };

        foreach (var map in MapsFound())
            yield return map;

        foreach (var command in Commands())
            yield return command;

        foreach (var (key, section) in SettingsFound())
            yield return new SearchItem(SearchScope.Settings, "setting:" + key, _localizer[key], _localizer["Section" + section], "General.Settings", "", "", () => ShowSettings(section));
    }

    // Edit's Undo and Redo follow the editor shown, and what it holds to take back or do again: in the world's tab, the route or the zone selected.
    private void FollowEditor(string? property)
    {
        if (property is nameof(WorldViewportViewModel.Route))
            Bottom.Follow(World.Route);

        if (property is not (nameof(EditorTabs.Selected) or nameof(WorldViewportViewModel.Route) or nameof(WorldViewportViewModel.Zone)))
            return;

        if (_followed is not null)
            _followed.PropertyChanged -= OnUndoableChanged;

        _followed = Undoable;

        if (_followed is not null)
            _followed.PropertyChanged += OnUndoableChanged;

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnUndoableChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(IUndoable.CanUndo) or nameof(IUndoable.CanRedo)))
            return;

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private IEnumerable<SearchItem> Commands()
    {
        SearchItem Command(string key, string keys, Action run)
        {
            return new SearchItem(SearchScope.Commands, "command:" + key, _localizer[key], "", null, "", keys, run);
        }

        yield return Command("CommandNewProfile", "Ctrl N", NewProfile);
        yield return Command("CommandSaveAll", "Ctrl S", SaveAll);
        yield return Command("CommandUndo", "Ctrl Z", Undo);
        yield return Command("CommandRedo", "Ctrl Y", Redo);
        yield return Command("CommandToggleJournal", "Ctrl J", ToggleBottom);
        yield return Command("CommandShowSequencer", "", ShowSequencer);
        yield return Command("CommandGoToAssets", _localizer["KeysCtrlSpace"], GoToAssets);
        yield return Command("CommandDefaultLayout", "", Layout.Reset);
        yield return Command("CommandPlay", "F5", RunBlock.PlayOrPause);
        yield return Command("CommandPause", "F6", RunBlock.Pause);
        yield return Command("CommandStop", _localizer["KeysShiftF5"], RunBlock.Stop);
        yield return Command("CommandSettings", "Ctrl ,", ShowSettings);
        yield return Command("CommandCloseTabs", "", Editor.CloseAll);
        yield return Command("CommandProjectFolder", "", OpenProjectFolder);
        yield return Command("CommandCheckUpdate", "", () => Updates.CheckCommand.Execute(null));
    }

    private void ChooseAsset(string id)
    {
        Properties.ShowAsset(id);
        Properties.Refresh(_host.Services);
    }

    private void ShowUsages(string id)
    {
        ChooseAsset(id);
        Layout.Show(StudioPanel.Details, true);
    }

    private void OpenProfileTab(ProfileViewModel item)
    {
        Editor.Open(ProfileTab + item.TabKey, () => new EditorTab(ProfileTab + item.TabKey, () => item.Title, "Nodes.Template", item, needsHost: true)
        {
            Closing = () => CloseProfile(item),
            Modified = () => item.IsDirty,
        });
        Editor.Refresh();
    }

    // A route or a zone opened is selected in the world, whose tab shows: the Details panel then shows it (D397).
    private void ShowRoute(RouteEditorViewModel item)
    {
        if (!Selection.IsRoute(item.Id))
            Selection.SelectRoute(item.Id);

        ShowWorld();
        ChooseAsset(item.Id);
    }

    private void ShowZone(ZoneEditorViewModel item)
    {
        if (!Selection.IsZone(item.Id))
            Selection.SelectZone(item.Id);

        ShowWorld();
        ChooseAsset(item.Id);
    }

    // An editor that ends, its asset gone, leaves nothing selected.
    private void Unselect(string id)
    {
        if (Selection.AssetId == id)
            Selection.Clear();
    }

    private void OnSelectionChanged()
    {
        switch (Selection.Kind)
        {
            case WorldSelectionKind.Character when Selection.Character is { } key:
                if (RunBlock.ChosenKey != key)
                    RunBlock.Choose(key);

                break;
            case WorldSelectionKind.Route or WorldSelectionKind.Zone when Selection.AssetId is { } id:
                ChooseAsset(id);

                break;
        }
    }

    private void OpenSequenceTab(SequenceEditorViewModel item)
    {
        Editor.Open(SequenceTab + item.TabKey, () => new EditorTab(SequenceTab + item.TabKey, () => item.Name, AssetKinds.IconOf(AssetKind.Behavior), item, needsHost: true)
        {
            Closing = () => CloseSequence(item),
            Modified = () => item.IsDirty,
        });
        Editor.Refresh();
    }

    private void CloseSequenceTab(SequenceEditorViewModel item)
    {
        Editor.Drop(Editor.Items.FirstOrDefault(tab => tab.Content == item));
    }

    // The tab closes once its editor does: at once without changes, else once the question asked is answered.
    private static bool CloseSequence(SequenceEditorViewModel item)
    {
        item.CloseCommand.Execute(null);

        return false;
    }

    private void CloseProfileTab(ProfileViewModel item)
    {
        Editor.Drop(Editor.Items.FirstOrDefault(tab => tab.Content == item));
    }

    // The tab closes once its editor does: at once without changes, else once the question asked is answered.
    private static bool CloseProfile(ProfileViewModel item)
    {
        if (item.IsDirty)
            item.CloseCommand.Execute(null);
        else
            item.Cancel();

        return false;
    }

    private void ShowJournalOf(CharacterKey key)
    {
        Bottom.Show(BottomTab.Journal);
        Bottom.Logs.Show(key);
    }

    private BotViewModel? BotOf(CharacterKey key)
    {
        return Bots.Items.FirstOrDefault(bot => bot.Bot.Key == key);
    }

    private CharacterTabViewModel TabOf(CharacterKey key)
    {
        if (!_characterTabs.TryGetValue(key, out var tab))
            _characterTabs[key] = tab = new CharacterTabViewModel(_localizer, key, Bots, OpenProfile, Bottom.ShowTrace);

        return tab;
    }

    private void Chose(CharacterKey? key)
    {
        if (key is { } selected)
            Selection.SelectCharacter(selected);
        else if (Selection.Kind is WorldSelectionKind.Character)
            Selection.Clear();

        Characters.Chosen = key;
        Bottom.Chosen = key;
        Properties.ShowCharacter(key);
        Characters.Refresh(_host.Services);
        Properties.Refresh(_host.Services);
    }

    private void ChooseAndOpen(CharacterKey key)
    {
        RunBlock.Choose(key);
        OpenCharacter(key);
    }

    private IEnumerable<SearchItem> MapsFound()
    {
        // The map shows in the tab of the character chosen, else of the first online, else of the first D3B knows (D334).
        if ((RunBlock.ChosenKey ?? Characters.Online.FirstOrDefault()?.Bot.Key ?? Characters.Offline.FirstOrDefault()?.Key) is not { } key)
            return [];

        var services = _host.Services;

        if (_maps.Count == 0 || !_localizer.Culture.Equals(_mapsCulture))
        {
            _mapsCulture = _localizer.Culture;
            _maps = [.. MapItems(services?.GetService<GameWorld>(), services?.GetService<GameTexts>())];
        }

        return _maps.Select(map => map with { Run = () => PreviewMap(map.Key, key) });
    }

    private IEnumerable<SearchItem> MapItems(GameWorld? world, GameTexts? texts)
    {
        if (world is null)
            yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < world.Atlas.Positions.Length; i++)
        {
            var position = world.Atlas.Positions.Span[i];
            var area = world.Atlas.TryGetSubArea(position.SubAreaId, out var subArea) && texts is not null && texts.TryGet(_localizer.GameLanguage, subArea.NameId, out var name) ? name : _localizer.Format("SubArea", position.SubAreaId);
            var coordinates = _localizer.Format("Coordinates", position.X, position.Y);

            // Maps of one place and coordinates, as floors, are one result: the first.
            if (seen.Add(coordinates + area))
                yield return new SearchItem(SearchScope.Maps, "map:" + position.MapId, $"{coordinates} · {area}", "", "Toolwindows.Web", "", "", () => { });
        }
    }

    private void PreviewMap(string mapKey, CharacterKey key)
    {
        var mapId = long.Parse(mapKey.AsSpan("map:".Length), System.Globalization.CultureInfo.InvariantCulture);

        RunBlock.Choose(key);
        OpenCharacter(key);
        TabOf(key).Map.Preview(mapId);
    }

    private void UseLanguage(CultureInfo culture)
    {
        _localizer.Use(culture);
        _preferences?.RememberLanguage(_localizer.Culture.TwoLetterISOLanguageName);
        Retell();
        Refresh();
    }
}
