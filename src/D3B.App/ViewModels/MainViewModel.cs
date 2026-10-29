using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.Bot.Pages;
using D3B.Data;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The app's window: where the bots' host stands, its pages in the rail, and the language (D14, D91, D96, D101).</summary>
internal sealed partial class MainViewModel : ObservableObject
{
    private readonly HostRunner _host;
    private readonly Localizer _localizer;
    private readonly AppPreferences? _preferences;
    private bool _pagesRead;

    /// <summary>Gets the bots page.</summary>
    public BotsViewModel Bots { get; }

    /// <summary>Gets the accounts page.</summary>
    public AccountsViewModel Accounts { get; }

    /// <summary>Gets the profiles page.</summary>
    public ProfilesViewModel Profiles { get; }

    /// <summary>Gets the pages the host's modules registered, in the rail after the app's own, once the host runs.</summary>
    public ObservableCollection<ModulePageViewModel> Pages { get; } = [];

    /// <summary>Gets whether the bots page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsBotsPage { get; private set; } = true;

    /// <summary>Gets whether the accounts page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsAccountsPage { get; private set; }

    /// <summary>Gets whether the profiles page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsProfilesPage { get; private set; }

    /// <summary>Gets the module's page the window shows, if it shows one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModulePage))]
    public partial ModulePageViewModel? SelectedPage { get; private set; }

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

    /// <summary>Gets the language the app is in, as a code.</summary>
    [ObservableProperty]
    public partial string LanguageCode { get; private set; } = "";

    /// <summary>Gets whether a module's page is the one the window shows.</summary>
    public bool IsModulePage => SelectedPage is not null;

    /// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
    /// <param name="host">The bots' host in the app's process.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="preferences">What the app remembers of its own, its language and theme, or <see langword="null"/> to remember nothing.</param>
    public MainViewModel(HostRunner host, Localizer localizer, AppPreferences? preferences = null)
    {
        _host = host;
        _localizer = localizer;
        _preferences = preferences;
        Bots = new BotsViewModel(localizer);
        Accounts = new AccountsViewModel(localizer);
        Profiles = new ProfilesViewModel(localizer);
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
        LanguageCode = _localizer["LanguageCode"];
        (HostTitle, HostTone) = status switch
        {
            HostStatus.Starting => (_localizer["HostStatusStarting"], Tone.Info),
            HostStatus.Running => (_localizer["HostStatusRunning"], Tone.Success),
            HostStatus.Failed => (_localizer["HostStatusFailed"], Tone.Danger),
            HostStatus.Stopping => (_localizer["HostStatusStopping"], Tone.Warning),
            _ => (_localizer["HostStatusStopped"], Tone.Neutral),
        };
        HostDetail = status switch
        {
            HostStatus.Starting => _localizer["HostPreparing"],
            HostStatus.Running => _localizer.Format("HostClient", services?.GetService<ClientBuild>()?.Version ?? "?"),
            HostStatus.Failed => _localizer["HostFailedDetail"],
            _ => "",
        };

        // The profiles first: the bots' cards offer them, and the accounts name them.
        Profiles.Refresh(services);
        Accounts.Refresh(services);
        Bots.Refresh(services);
        RefreshPages(services);
    }

    /// <summary>Replaces the module's page whose own code threw what nothing else caught on the UI's thread.</summary>
    /// <param name="exception">What was thrown.</param>
    /// <returns>Whether a page's code threw it, and the page now shows it instead: else the app's own fault, not handled.</returns>
    public bool FailPageThatThrew(Exception exception)
    {
        return Pages.Any(page => page.TryFailWith(exception));
    }

    /// <summary>Remembers the theme the window switched to.</summary>
    /// <param name="dark">Whether it is dark.</param>
    public void RememberTheme(bool dark)
    {
        _preferences?.RememberTheme(dark ? AppPreferences.Dark : AppPreferences.Light);
    }

    private void RefreshPages(IServiceProvider? services)
    {
        // The catalogue is the host's: read once the host runs, and kept after it stops, as the bots shown are.
        if (!_pagesRead && IsRunning && services is not null)
        {
            foreach (var definition in services.GetService<PageCatalog>()?.All ?? [])
                Pages.Add(new ModulePageViewModel(definition, services, _localizer, Select));

            _pagesRead = true;
            HasPages = Pages.Count > 0;
        }

        foreach (var page in Pages)
            page.Refresh();
    }

    [RelayCommand]
    private void ShowBots()
    {
        Select(null);
    }

    [RelayCommand]
    private void ShowAccounts()
    {
        Select(null);
        IsBotsPage = false;
        IsAccountsPage = true;
    }

    [RelayCommand]
    private void ShowProfiles()
    {
        Select(null);
        IsBotsPage = false;
        IsProfilesPage = true;
    }

    private void Select(ModulePageViewModel? page)
    {
        foreach (var other in Pages)
            other.IsSelected = other == page;

        SelectedPage = page;
        IsBotsPage = page is null;
        IsAccountsPage = false;
        IsProfilesPage = false;
        page?.Refresh();
    }

    [RelayCommand]
    private void SwitchLanguage()
    {
        _localizer.Use(_localizer.Culture.Equals(Localizer.French) ? Localizer.English : Localizer.French);
        _preferences?.RememberLanguage(_localizer.Culture.TwoLetterISOLanguageName);
        Refresh();
    }
}
