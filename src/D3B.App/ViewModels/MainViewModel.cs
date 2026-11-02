using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.Updates;
using D3B.App.ViewModels.Inspector;
using D3B.App.ViewModels.Logs;
using D3B.App.ViewModels.Maps;
using D3B.App.ViewModels.Statistics;
using D3B.App.ViewModels.Studio;
using D3B.Bot.Assets;
using D3B.Bot.Characters;
using D3B.Bot.Pages;
using D3B.Data;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The studio's window: where the bots' host stands, its docks and editor, the run block, the search, its dialogs and toasts (D14, D91, D320 to D322, D328).</summary>
/// <remarks>
/// Until 7c3 and 7c4 draw them again, each page of D101 and D162 has its place in the docks (D333): the bots' cards in the
/// characters' panel, the journal, the protocol and the statistics in the bottom panel, the accounts, the profiles, the map,
/// the settings and the modules' pages in the editor's tabs.
/// </remarks>
internal sealed partial class MainViewModel : ObservableObject
{
    private const string WelcomeTab = "welcome";
    private const string AccountsTab = "accounts";
    private const string ProfilesTab = "profiles";
    private const string MapTab = "map";
    private const string SettingsTab = "settings";

    private readonly HostRunner _host;
    private readonly Localizer _localizer;
    private readonly AppPreferences? _preferences;
    private readonly Action _close;
    private bool _pagesRead;

    /// <summary>Gets the dialog the window shows over the studio.</summary>
    public Dialogs Dialogs { get; } = new();

    /// <summary>Gets what the window says in its toasts.</summary>
    public Toasts Toasts { get; } = new();

    /// <summary>Gets the docks' layout.</summary>
    public StudioLayout Layout { get; }

    /// <summary>Gets the editor's tabs.</summary>
    public EditorTabs Editor { get; } = new();

    /// <summary>Gets the title row's run block.</summary>
    public RunBlockViewModel RunBlock { get; }

    /// <summary>Gets the search, Ctrl K.</summary>
    public SearchViewModel Search { get; }

    /// <summary>Gets the bots' cards, in the characters' panel until 7c3.</summary>
    public BotsViewModel Bots { get; }

    /// <summary>Gets the accounts' page, a tab until 7c3.</summary>
    public AccountsViewModel Accounts { get; }

    /// <summary>Gets the profiles' page, a tab until 7c4.</summary>
    public ProfilesViewModel Profiles { get; }

    /// <summary>Gets the map's page, a tab until 7c3.</summary>
    public MapViewModel Map { get; }

    /// <summary>Gets the statistics, in the bottom panel.</summary>
    public StatisticsViewModel Statistics { get; }

    /// <summary>Gets the journal, in the bottom panel.</summary>
    public LogsViewModel Logs { get; }

    /// <summary>Gets the protocol, in the bottom panel.</summary>
    public InspectorViewModel Inspector { get; }

    /// <summary>Gets the settings, a tab of the editor (D329).</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Gets the app's version and its update, in the status bar and the settings (D173).</summary>
    public UpdatesViewModel Updates { get; }

    /// <summary>Gets the pages the host's modules registered, each opened in a tab of the editor.</summary>
    public ObservableCollection<ModulePageViewModel> Pages { get; } = [];

    /// <summary>Gets or sets what opens a folder in the system's file manager, as the window can.</summary>
    public Action<string>? OpenFolder { get; set; }

    /// <summary>Gets the bottom panel's tab shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJournalShown), nameof(IsTraceShown), nameof(IsProblemsShown), nameof(IsProtocolShown), nameof(IsStatisticsShown))]
    public partial BottomTab BottomTab { get; private set; }

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

    /// <summary>Gets whether everything is saved: no profile edited with changes not saved.</summary>
    [ObservableProperty]
    public partial bool IsSaved { get; private set; } = true;

    /// <summary>Gets whether the host does not run, which the panels that read it say.</summary>
    public bool IsNotRunning => !IsRunning;

    /// <summary>Gets whether the bottom panel shows the journal.</summary>
    public bool IsJournalShown => BottomTab is BottomTab.Journal;

    /// <summary>Gets whether the bottom panel shows the trace.</summary>
    public bool IsTraceShown => BottomTab is BottomTab.Trace;

    /// <summary>Gets whether the bottom panel shows the problems.</summary>
    public bool IsProblemsShown => BottomTab is BottomTab.Problems;

    /// <summary>Gets whether the bottom panel shows the protocol.</summary>
    public bool IsProtocolShown => BottomTab is BottomTab.Protocol;

    /// <summary>Gets whether the bottom panel shows the statistics.</summary>
    public bool IsStatisticsShown => BottomTab is BottomTab.Statistics;

    /// <summary>Gets the app's version and the client build it ships, under the welcome's logo.</summary>
    public string VersionText => _localizer.Format("WelcomeVersion", Updates.Version, Updates.ClientVersion);

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
        Bots = new BotsViewModel(localizer, ShowJournalOf, ShowMapOf, Toasts);
        Accounts = new AccountsViewModel(localizer, Dialogs);
        Profiles = new ProfilesViewModel(localizer, Dialogs, Toasts);
        Map = new MapViewModel(localizer, choice);
        Statistics = new StatisticsViewModel(localizer);
        Logs = new LogsViewModel(localizer, choice, Toasts);
        Inspector = new InspectorViewModel(localizer, choice);
        Updates = new UpdatesViewModel(localizer, updates, Dialogs, () => IsRunning && Bots.HasItems, _close);
        Settings = new SettingsViewModel(localizer, preferences, UseLanguage, Toasts, Updates);
        RunBlock = new RunBlockViewModel(Bots, localizer);
        Search = new SearchViewModel(localizer, SearchItems);
        Bots.Choose = RunBlock.Choose;
        ClientText = localizer.Format("StatusClient", Updates.ClientVersion);
        ShowWelcome();
    }

    private static IEnumerable<(string Key, string Section)> SettingsFound()
    {
        yield return ("SettingDisplay", "SectionBots");
        yield return ("SettingDelays", "SectionBots");
        yield return ("SettingCaptures", "SectionNetwork");
        yield return ("SettingLanguage", "SectionAppearance");
        yield return ("SettingFiles", "SectionFiles");
        yield return ("SettingUpdate", "SectionAbout");
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

        // The profiles first: the bots' cards offer them, and the accounts name them.
        Profiles.Refresh(services);
        Accounts.Refresh(services);
        Bots.Refresh(services);
        RunBlock.Refresh();
        OnlineText = _localizer.Format("CharactersOnline", RunBlock.Online.Count);
        IsSaved = Profiles.Editing is not { IsDirty: true };

        // Only what shows reads what came since: the rest reads it all once shown again.
        if (Editor.IsShown(MapTab))
            Map.Refresh(services);

        if (Layout.IsBottomOpen && IsStatisticsShown)
            Statistics.Refresh(services);

        if (Layout.IsBottomOpen && IsJournalShown)
            Logs.Refresh(services);

        if (Layout.IsBottomOpen && IsProtocolShown)
            Inspector.Refresh(services);

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

    /// <summary>Shows a tab of the bottom panel, the panel opened if it was closed.</summary>
    /// <param name="tab">The tab.</param>
    [RelayCommand]
    public void ShowBottom(BottomTab tab)
    {
        BottomTab = tab;

        if (!Layout.IsBottomOpen)
            Layout.Show(StudioPanel.Bottom, true);

        RefreshBottom();
    }

    /// <summary>Shows the bottom panel, or closes it: Ctrl J.</summary>
    [RelayCommand]
    public void ToggleBottom()
    {
        Layout.Toggle(StudioPanel.Bottom);
        RefreshBottom();
    }

    /// <summary>Opens the assets: Ctrl Space (D321).</summary>
    [RelayCommand]
    public void GoToAssets()
    {
        Layout.Show(StudioPanel.Assets, true);
    }

    /// <summary>Shows the welcome's tab.</summary>
    [RelayCommand]
    public void ShowWelcome()
    {
        Editor.Open(WelcomeTab, () => new EditorTab(WelcomeTab, () => _localizer["Welcome"], null, this, needsHost: false));
    }

    /// <summary>Shows the accounts' tab.</summary>
    [RelayCommand]
    public void ShowAccounts()
    {
        Editor.Open(AccountsTab, () => new EditorTab(AccountsTab, () => _localizer["Accounts"], "General.User", Accounts, needsHost: true));
    }

    /// <summary>Shows the profiles' tab.</summary>
    [RelayCommand]
    public void ShowProfiles()
    {
        Editor.Open(ProfilesTab, () => new EditorTab(ProfilesTab, () => _localizer["Profiles"], "Nodes.Template", Profiles, needsHost: true));
    }

    /// <summary>Shows the map's tab.</summary>
    [RelayCommand]
    public void ShowMap()
    {
        Editor.Open(MapTab, () => new EditorTab(MapTab, () => _localizer["Map"], "Toolwindows.Web", Map, needsHost: true));
        Map.Refresh(_host.Services);
    }

    /// <summary>Shows the settings' tab: the title's gear, Ctrl , (D329).</summary>
    [RelayCommand]
    public void ShowSettings()
    {
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
        ShowProfiles();

        if (IsRunning)
            Profiles.NewCommand.Execute(null);
    }

    /// <summary>Saves what is edited: File's Save all, Ctrl S.</summary>
    [RelayCommand]
    public void SaveAll()
    {
        if (Profiles.Editing is { } editing && editing.SaveCommand.CanExecute(null))
            editing.SaveCommand.Execute(null);
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

    private void RefreshBottom()
    {
        var services = _host.Services;

        if (!Layout.IsBottomOpen)
            return;

        switch (BottomTab)
        {
            case BottomTab.Journal:
                Logs.Refresh(services);
                break;
            case BottomTab.Protocol:
                Inspector.Refresh(services);
                break;
            case BottomTab.Statistics:
                Statistics.Refresh(services);
                break;
        }
    }

    private IEnumerable<SearchItem> SearchItems()
    {
        foreach (var profile in Profiles.Items)
        {
            yield return new SearchItem(SearchScope.Assets, "profile:" + profile.Id, profile.Name, _localizer.Format("SearchProfile", profile.ActivityName), "Nodes.Template", "", "", () =>
            {
                ShowProfiles();

                if (profile.EditCommand.CanExecute(null))
                    profile.EditCommand.Execute(null);
            });
        }

        foreach (var bot in Bots.Items)
            yield return new SearchItem(SearchScope.Characters, "character:" + bot.Bot.Key, bot.Name, bot.IsConnected ? bot.Level : _localizer["Disconnected"], null, bot.Initials, "", () => RunBlock.Choose(bot));

        foreach (var command in Commands())
            yield return command;

        foreach (var (key, section) in SettingsFound())
            yield return new SearchItem(SearchScope.Settings, "setting:" + key, _localizer[key], _localizer[section], "General.Settings", "", "", ShowSettings);
    }

    private IEnumerable<SearchItem> Commands()
    {
        SearchItem Command(string key, string keys, Action run)
        {
            return new SearchItem(SearchScope.Commands, "command:" + key, _localizer[key], "", null, "", keys, run);
        }

        yield return Command("CommandNewProfile", "Ctrl N", NewProfile);
        yield return Command("CommandSaveAll", "Ctrl S", SaveAll);
        yield return Command("CommandToggleJournal", "Ctrl J", ToggleBottom);
        yield return Command("CommandGoToAssets", _localizer["KeysCtrlSpace"], GoToAssets);
        yield return Command("CommandDefaultLayout", "", Layout.Reset);
        yield return Command("CommandPlay", "F5", RunBlock.PlayOrPause);
        yield return Command("CommandPause", "F6", RunBlock.Pause);
        yield return Command("CommandStop", _localizer["KeysShiftF5"], RunBlock.Stop);
        yield return Command("CommandSettings", "Ctrl ,", ShowSettings);
        yield return Command("CommandAccounts", "", ShowAccounts);
        yield return Command("CommandProfiles", "", ShowProfiles);
        yield return Command("CommandMap", "", ShowMap);
        yield return Command("CommandCloseTabs", "", Editor.CloseAll);
        yield return Command("CommandProjectFolder", "", OpenProjectFolder);
        yield return Command("CommandCheckUpdate", "", () => Updates.CheckCommand.Execute(null));
    }

    private void ShowJournalOf(CharacterKey key)
    {
        ShowBottom(BottomTab.Journal);
        Logs.Show(key);
    }

    private void ShowMapOf(CharacterKey key)
    {
        ShowMap();
        Map.Show(key);
    }

    private void UseLanguage(CultureInfo culture)
    {
        _localizer.Use(culture);
        _preferences?.RememberLanguage(_localizer.Culture.TwoLetterISOLanguageName);
        Retell();
        Refresh();
    }
}
