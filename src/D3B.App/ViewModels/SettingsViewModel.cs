using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels.AppSettings;
using D3B.Bot.Banks;
using D3B.Bot.Data;
using D3B.Bot.Runtime;
using D3B.Network.Relay;
using D3B.Storage;
using D3B.Studio.Assets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The settings' tab, in sections: Bots, Network, Appearance, Files and About (D151, D329).</summary>
/// <remarks>
/// <para>
/// The settings are the machine's, not a profile's, kept in the app's own file (<see cref="AppPreferences"/>) as the
/// host's configuration names them, which the app gives the host as it starts. The bots' delays apply at once, to every
/// bot's next wait; the relay's and the captures from the host's next start, the relay being built once.
/// </para>
/// <para>
/// The tab shows the delays the bots wait once the host runs, which the host's command line may have raised; changed
/// before it ran, the tab's are given to the bots instead.
/// </para>
/// <para>
/// One section shows at a time, chosen in its column or by the search's settings; the tab's own search keeps the rows
/// whose title or line hold its text, and the sections that keep one.
/// </para>
/// </remarks>
internal sealed partial class SettingsViewModel : ObservableObject
{
    private const string NoticesFile = "THIRD-PARTY-NOTICES.md";
    private const decimal DefaultCaptureDays = 7;
    private const decimal DefaultCaptureMegabytes = 1024;
    private const string NoticesPage = "https://github.com/AerafalDev/D3B/blob/main/THIRD-PARTY-NOTICES.md";

    private readonly Localizer _localizer;
    private readonly AppPreferences? _preferences;
    private readonly Action<CultureInfo> _useLanguage;
    private readonly Toasts _toasts;
    private readonly string? _storedCaptures;
    private BotDelays _delays;
    private BankingPreferences? _bankingLive;
    private bool _bankingChanged;
    private RelayTimeouts _timeouts;
    private Bots? _bots;
    private bool _changed;
    private bool _remembers;
    private CultureInfo? _labelled;

    /// <summary>Gets the sections, in their column's order.</summary>
    public IReadOnlyList<SettingsSectionViewModel> Sections { get; }

    /// <summary>Gets the section Bots.</summary>
    public SettingsSectionViewModel Bots => Sections[(int)SettingsSection.Bots];

    /// <summary>Gets the section Network.</summary>
    public SettingsSectionViewModel Network => Sections[(int)SettingsSection.Network];

    /// <summary>Gets the section Appearance.</summary>
    public SettingsSectionViewModel Appearance => Sections[(int)SettingsSection.Appearance];

    /// <summary>Gets the section Files.</summary>
    public SettingsSectionViewModel Files => Sections[(int)SettingsSection.Files];

    /// <summary>Gets the section About.</summary>
    public SettingsSectionViewModel About => Sections[(int)SettingsSection.About];

    /// <summary>Gets the client's display time, after a map and after a window.</summary>
    public DelayViewModel Display { get; }

    /// <summary>Gets the bots' other delays, which apply at once.</summary>
    public IReadOnlyList<DelayViewModel> BotWaits { get; }

    /// <summary>Gets the relay's timeouts, which apply from the host's next start.</summary>
    public IReadOnlyList<DelayViewModel> RelayWaits { get; }

    /// <summary>Gets the row of the share of pods at which the bots bank (D405).</summary>
    public SettingsRowViewModel BankFullAtRow { get; }

    /// <summary>Gets the row of whether the bots take a zaap to the bank (D405).</summary>
    public SettingsRowViewModel BankZaapsRow { get; }

    /// <summary>Gets the least share of pods the bots may bank at, for the box.</summary>
    public decimal BankFullAtMinimum { get; } = BankingPreferences.LeastFullAt;

    /// <summary>Gets the most share of pods the bots may bank at, for the box.</summary>
    public decimal BankFullAtMaximum { get; } = BankingPreferences.MostFullAt;

    /// <summary>Gets the row of the captures.</summary>
    public SettingsRowViewModel Captures { get; }

    /// <summary>Gets the row of the captures' folder.</summary>
    public SettingsRowViewModel CapturesFolder { get; }

    /// <summary>Gets the row of how many days captures are kept (D409).</summary>
    public SettingsRowViewModel CaptureDaysRow { get; }

    /// <summary>Gets the row of how many megabytes captures may take (D409).</summary>
    public SettingsRowViewModel CaptureMegabytesRow { get; }

    /// <summary>Gets the row of the language.</summary>
    public SettingsRowViewModel Language { get; }

    /// <summary>Gets the row of the docks' layout.</summary>
    public SettingsRowViewModel Layout { get; }

    /// <summary>Gets the row of the project's folder.</summary>
    public SettingsRowViewModel Project { get; }

    /// <summary>Gets the row of the database.</summary>
    public SettingsRowViewModel Database { get; }

    /// <summary>Gets the row of the client's data.</summary>
    public SettingsRowViewModel ClientData { get; }

    /// <summary>Gets the row of the app's own preferences.</summary>
    public SettingsRowViewModel Preferences { get; }

    /// <summary>Gets the row of the app's version and update.</summary>
    public SettingsRowViewModel Version { get; }

    /// <summary>Gets the row of the licences.</summary>
    public SettingsRowViewModel Licences { get; }

    /// <summary>Gets the section shown.</summary>
    [ObservableProperty]
    public partial SettingsSectionViewModel Selected { get; private set; }

    /// <summary>Gets or sets the text the rows are searched by.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    /// <summary>Gets whether the search leaves no row.</summary>
    [ObservableProperty]
    public partial bool HasNoMatch { get; private set; }

    /// <summary>Gets or sets whether the relay records the game connection from the host's next start; on by default (D409).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCapturesOff))]
    [NotifyCanExecuteChangedFor(nameof(ResetCapturesCommand))]
    public partial bool RecordsCaptures { get; set; } = true;

    /// <summary>Gets or sets how many days captures are kept, 0 for as long as they fit; emptied, it keeps the days it had (D409).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCaptureDaysChanged), nameof(RetentionText))]
    [NotifyCanExecuteChangedFor(nameof(ResetCaptureDaysCommand))]
    public partial decimal? CaptureDays { get; set; } = DefaultCaptureDays;

    /// <summary>Gets or sets how many megabytes captures may take together, 0 for no limit; emptied, it keeps the size it had (D409).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCaptureMegabytesChanged), nameof(RetentionText))]
    [NotifyCanExecuteChangedFor(nameof(ResetCaptureMegabytesCommand))]
    public partial decimal? CaptureMegabytes { get; set; } = DefaultCaptureMegabytes;

    /// <summary>Gets the least a retention can be: 0, which lifts its limit.</summary>
    public decimal CaptureLimitMinimum { get; } = 0;

    /// <summary>Gets the most days captures can be kept, ten years.</summary>
    public decimal CaptureDaysMaximum { get; } = 3650;

    /// <summary>Gets the most megabytes captures can take, a terabyte.</summary>
    public decimal CaptureMegabytesMaximum { get; } = 1_048_576;

    /// <summary>Gets whether the game connection is not recorded, which is not the default.</summary>
    public bool IsCapturesOff => !RecordsCaptures;

    /// <summary>Gets whether the days differ from their default.</summary>
    public bool IsCaptureDaysChanged => CaptureDays is { } days && days != DefaultCaptureDays;

    /// <summary>Gets whether the megabytes differ from their default.</summary>
    public bool IsCaptureMegabytesChanged => CaptureMegabytes is { } megabytes && megabytes != DefaultCaptureMegabytes;

    /// <summary>Gets how long and how much of the captures are kept, in words.</summary>
    public string RetentionText => RetentionOf(CaptureDays ?? DefaultCaptureDays, CaptureMegabytes ?? DefaultCaptureMegabytes);

    /// <summary>Gets or sets the share of its pods, in percent, at which a bot banks, unless its profile asks for its own; emptied, it keeps the share it had (D405).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetBankFullAtCommand))]
    public partial decimal? BankFullAt { get; set; }

    /// <summary>Gets or sets whether a bot may take a zaap to the bank, unless its profile asks for its own rules (D405).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetBankZaapsCommand))]
    public partial bool BankUseZaaps { get; set; } = true;

    /// <summary>Gets whether the share of pods differs from its default.</summary>
    public bool IsBankFullAtChanged => BankFullAt is { } share && share != BankingPreferences.DefaultFullAt;

    /// <summary>Gets whether the search keeps a row of the banking group.</summary>
    public bool IsBankingShown => BankFullAtRow.IsShown || BankZaapsRow.IsShown;

    /// <summary>Gets whether the bots are kept from taking a zaap to the bank.</summary>
    public bool IsBankZaapsOff => !BankUseZaaps;

    /// <summary>Gets whether the host records captures now, and where.</summary>
    [ObservableProperty]
    public partial string CapturesNow { get; private set; } = "";

    /// <summary>Gets the folder the captures are recorded in, or would be.</summary>
    [ObservableProperty]
    public partial string CapturesPath { get; private set; } = RelaySettings.DefaultCapturesDirectory;

    /// <summary>Gets the client build the app ships, said under its version.</summary>
    [ObservableProperty]
    public partial string ClientLine { get; private set; } = "";

    /// <summary>Gets whether the app is in French.</summary>
    [ObservableProperty]
    public partial bool IsFrench { get; private set; }

    /// <summary>Gets whether the app is in English.</summary>
    [ObservableProperty]
    public partial bool IsEnglish { get; private set; }

    /// <summary>Gets whether a delay was raised above its default, which the tab restores.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreDelaysCommand))]
    public partial bool HasRaisedDelays { get; private set; }

    /// <summary>Gets the project's folder, its assets a file each (D296).</summary>
    [ObservableProperty]
    public partial string ProjectPath { get; private set; } = StudioSettings.DefaultPath;

    /// <summary>Gets D3B's database file.</summary>
    [ObservableProperty]
    public partial string DatabasePath { get; private set; } = StorageSettings.DefaultPath;

    /// <summary>Gets the installed build's pack, once the host knows it, else that it is not known yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClientDataPath))]
    public partial string PackPath { get; private set; } = "";

    /// <summary>Gets whether the host knows the installed build's pack.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClientDataPath))]
    public partial bool IsPackKnown { get; private set; }

    /// <summary>Gets the folder of the installed build's pack, schema and mapping, once known, else that it is not known yet.</summary>
    public string ClientDataPath => IsPackKnown ? Path.GetDirectoryName(PackPath) ?? PackPath : PackPath;

    /// <summary>Gets the file the app keeps its settings in.</summary>
    public string SettingsPath { get; }

    /// <summary>Gets D3B's licence and those of what it uses: the file published beside the app, else the repository's.</summary>
    public string LicencesPath { get; }

    /// <summary>Gets the app's version and its update (D173).</summary>
    public UpdatesViewModel Updates { get; }

    /// <summary>Gets or sets what opens a folder in the system's file manager, as the window can.</summary>
    public Action<string>? OpenFolder { get; set; }

    /// <summary>Gets or sets what opens a file or a page with the system's own application, as the window can.</summary>
    public Action<string>? OpenFile { get; set; }

    /// <summary>Gets or sets what gives the docks their default layout back.</summary>
    public Action? ResetLayout { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SettingsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="preferences">What the app remembers, or <see langword="null"/> to remember nothing.</param>
    /// <param name="useLanguage">Puts the app in a language, as its title bar does.</param>
    /// <param name="toasts">What the window says in its toasts, such as the delays restored.</param>
    /// <param name="updates">The app's version and its update, or <see langword="null"/> for an app that never updates.</param>
    public SettingsViewModel(Localizer localizer, AppPreferences? preferences, Action<CultureInfo> useLanguage, Toasts toasts, UpdatesViewModel? updates = null)
    {
        _localizer = localizer;
        _toasts = toasts;
        _preferences = preferences;
        _useLanguage = useLanguage;
        Updates = updates ?? new UpdatesViewModel(localizer, null, new Dialogs(), () => false, () => { });

        var stored = new ConfigurationBuilder().AddInMemoryCollection(preferences?.Host ?? new Dictionary<string, string?>()).Build();
        var notices = Path.Combine(AppContext.BaseDirectory, NoticesFile);

        _delays = Stored(() => BotDelays.Read(stored), BotDelays.Measured);
        _timeouts = Stored(() => RelayTimeouts.Read(stored), RelayTimeouts.Default);

        var banking = Stored(() => BankingPreferences.Read(stored), new BankingPreferences());

        BankFullAt = banking.FullAt;
        BankUseZaaps = banking.UseZaaps;
        _storedCaptures = stored[RelaySettings.CapturesDirectoryKey];
        RecordsCaptures = !bool.TryParse(stored[RelaySettings.GameCapturesEnabledKey], out var game) || game || (bool.TryParse(stored[RelaySettings.CapturesEnabledKey], out var all) && all);
        CaptureDays = decimal.TryParse(stored[RelaySettings.CapturesRetentionDaysKey], NumberStyles.None, CultureInfo.InvariantCulture, out var days) ? days : DefaultCaptureDays;
        CaptureMegabytes = decimal.TryParse(stored[RelaySettings.CapturesRetentionMegabytesKey], NumberStyles.None, CultureInfo.InvariantCulture, out var megabytes) ? megabytes : DefaultCaptureMegabytes;
        SettingsPath = preferences?.Path ?? AppPreferences.DefaultPath;
        LicencesPath = File.Exists(notices) ? notices : NoticesPage;
        PackPath = localizer["PackPathUnknown"];
        ClientLine = localizer.Format("AboutClientLine", Updates.ClientVersion);
        Sections = [.. Enum.GetValues<SettingsSection>().Select(section => new SettingsSectionViewModel(localizer, section))];
        Selected = Sections[0];
        Selected.IsSelected = true;
        BankFullAtRow = new SettingsRowViewModel(localizer, "SettingBankFullAtRow");
        BankZaapsRow = new SettingsRowViewModel(localizer, "SettingBankZaapsRow");
        Captures = new SettingsRowViewModel(localizer, "SettingCapturesRow");
        CapturesFolder = new SettingsRowViewModel(localizer, "SettingCapturesFolder", hasDetail: false);
        CaptureDaysRow = new SettingsRowViewModel(localizer, "SettingCaptureDaysRow");
        CaptureMegabytesRow = new SettingsRowViewModel(localizer, "SettingCaptureMegabytesRow");
        Language = new SettingsRowViewModel(localizer, "SettingLanguageRow", hasDetail: false);
        Layout = new SettingsRowViewModel(localizer, "SettingLayout");
        Project = new SettingsRowViewModel(localizer, "SettingProject");
        Database = new SettingsRowViewModel(localizer, "SettingDatabase");
        ClientData = new SettingsRowViewModel(localizer, "SettingClientData");
        Preferences = new SettingsRowViewModel(localizer, "SettingPreferences");
        Version = new SettingsRowViewModel(localizer, "SettingVersion", hasDetail: false);
        Licences = new SettingsRowViewModel(localizer, "SettingLicences");
        Display = BotDelay("DelayDisplay", delays => delays.Display, (delays, delay) => delays with { Display = delay });
        BotWaits =
        [
            BotDelay("DelayAnswer", delays => delays.Answer, (delays, delay) => delays with { Answer = delay }),
            BotDelay("DelayWalkArrival", delays => delays.WalkArrival, (delays, delay) => delays with { WalkArrival = delay }),
            BotDelay("DelayWalkStep", delays => delays.WalkStep, (delays, delay) => delays with { WalkStep = delay }),
            BotDelay("DelayPlaced", delays => delays.Placed, (delays, delay) => delays with { Placed = delay }),
            BotDelay("DelayUseEnd", delays => delays.UseEnd, (delays, delay) => delays with { UseEnd = delay }),
            BotDelay("DelayBankFollow", delays => delays.BankFollow, (delays, delay) => delays with { BankFollow = delay }),
            BotDelay("DelayRecovery", delays => delays.Recovery, (delays, delay) => delays with { Recovery = delay }),
            BotDelay("DelayBetweenSteps", delays => delays.BetweenSteps, (delays, delay) => delays with { BetweenSteps = delay }),
            BotDelay("DelayFightAction", delays => delays.FightAction, (delays, delay) => delays with { FightAction = delay }),
        ];
        RelayWaits =
        [
            RelayDelay("DelayConnect", timeouts => timeouts.Connect, (timeouts, delay) => timeouts with { Connect = delay }),
            RelayDelay("DelayClose", timeouts => timeouts.Close, (timeouts, delay) => timeouts with { Close = delay }),
            RelayDelay("DelayHold", timeouts => timeouts.Hold, (timeouts, delay) => timeouts with { Hold = delay }),
            RelayDelay("DelaySelection", timeouts => timeouts.Selection, (timeouts, delay) => timeouts with { Selection = delay }),
        ];
        HasRaisedDelays = AnyRaised();
        _labelled = localizer.Culture;
        _remembers = true;
        MarkChanges();
    }

    /// <summary>Reads the host again, in the language chosen.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>.</param>
    public void Refresh(IServiceProvider? services)
    {
        IsFrench = _localizer.Culture.Equals(Localizer.French);
        IsEnglish = !IsFrench;

        if (!_localizer.Culture.Equals(_labelled))
        {
            foreach (var delay in Delays())
                delay.Relabel();

            foreach (var row in Rows())
                row.Relabel();

            foreach (var section in Sections)
                section.Relabel();

            ClientLine = _localizer.Format("AboutClientLine", Updates.ClientVersion);
            _labelled = _localizer.Culture;
            Search();
        }

        if (_bots is null && services?.GetService<Bots>() is { } bots)
            Attach(bots);

        if (_bankingLive is null && services?.GetService<BankingPreferences>() is { } banking)
            AttachBanking(banking);

        var relay = services?.GetService<RelaySettings>();

        if (relay is null)
            CapturesNow = "";
        else if (relay.GameCapturesDirectory is { } directory)
            CapturesNow = _localizer.Format("CapturesRecording", directory);
        else
            CapturesNow = _localizer["CapturesNotRecording"];

        // The game's captures are the ones a player reports and replays: their folder is the one shown.
        CapturesPath = Path.Combine(relay?.CapturesDirectory ?? (string.IsNullOrWhiteSpace(_storedCaptures) ? RelaySettings.DefaultCapturesDirectory : _storedCaptures), "game");
        ProjectPath = services?.GetService<AssetLibrary>()?.Folder ?? services?.GetService<StudioSettings>()?.Path ?? StudioSettings.DefaultPath;
        DatabasePath = services?.GetService<StorageSettings>()?.Path ?? StorageSettings.DefaultPath;

        var pack = services?.GetService<PackSource>()?.PackPath;

        IsPackKnown = pack is not null;
        PackPath = pack ?? _localizer["PackPathUnknown"];
    }

    /// <summary>Shows a section, the tab's search cleared if it hid it, as the search's settings ask.</summary>
    /// <param name="section">The section.</param>
    [RelayCommand]
    public void Show(SettingsSection section)
    {
        if (!Sections[(int)section].IsShown)
            Filter = "";

        Select(Sections[(int)section]);
    }

    /// <summary>Says a path was copied.</summary>
    public void Copied()
    {
        _toasts.Show(new Toast(_localizer["Copied"], Tone.Success));
    }

    private static T Stored<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (InvalidOperationException)
        {
            // A file edited by hand to a delay out of its bounds: the host says so as it starts; the tab offers D3B's.
            return fallback;
        }
    }

    private DelayViewModel BotDelay(string key, Func<BotDelays, TimeSpan> read, Func<BotDelays, TimeSpan, BotDelays> write)
    {
        return new DelayViewModel(_localizer, key, "DelayMeasured", (read(BotDelays.Measured), read(BotDelays.Most)), () => read(_delays), delay => UseDelays(write(_delays, delay)));
    }

    private DelayViewModel RelayDelay(string key, Func<RelayTimeouts, TimeSpan> read, Func<RelayTimeouts, TimeSpan, RelayTimeouts> write)
    {
        return new DelayViewModel(_localizer, key, "DelayDefault", (read(RelayTimeouts.Default), read(RelayTimeouts.Most)), () => read(_timeouts), delay => UseTimeouts(write(_timeouts, delay)));
    }

    private IEnumerable<DelayViewModel> Delays()
    {
        return BotWaits.Concat(RelayWaits).Prepend(Display);
    }

    private IEnumerable<SettingsRowViewModel> Rows()
    {
        return [BankFullAtRow, BankZaapsRow, Captures, CaptureDaysRow, CaptureMegabytesRow, CapturesFolder, Language, Layout, Project, Database, ClientData, Preferences, Version, Licences];
    }

    private void Select(SettingsSectionViewModel section)
    {
        foreach (var other in Sections)
            other.IsSelected = other == section;

        Selected = section;
    }

    private void Search()
    {
        var filter = Filter.Trim();

        foreach (var delay in Delays())
            delay.IsShown = delay.Holds(filter);

        foreach (var row in Rows())
            row.IsShown = row.Holds(filter);

        OnPropertyChanged(nameof(IsBankingShown));
        Bots.IsShown = Delays().Any(delay => delay.IsShown) || IsBankingShown;
        Network.IsShown = Captures.IsShown || CaptureDaysRow.IsShown || CaptureMegabytesRow.IsShown || CapturesFolder.IsShown;
        Appearance.IsShown = Language.IsShown || Layout.IsShown;
        Files.IsShown = Project.IsShown || Database.IsShown || ClientData.IsShown || Preferences.IsShown;
        About.IsShown = Version.IsShown || Licences.IsShown;
        HasNoMatch = !Sections.Any(section => section.IsShown);

        // The section shown stays while it keeps a row; else the first that keeps one shows.
        if (!Selected.IsShown && Sections.FirstOrDefault(section => section.IsShown) is { } first)
            Select(first);
    }

    private void MarkChanges()
    {
        Bots.HasChanges = Display.IsRaised || HasRaisedDelays || IsBankFullAtChanged || !BankUseZaaps;
        Network.HasChanges = IsCapturesOff || IsCaptureDaysChanged || IsCaptureMegabytesChanged;
    }

    private void Attach(Bots bots)
    {
        // Changed while the host got ready, the tab's delays win; else the bots', which its command line may raise.
        if (_changed)
            bots.Delays = _delays;
        else
            _delays = bots.Delays;

        _bots = bots;

        foreach (var delay in BotWaits.Prepend(Display))
            delay.Reread();

        HasRaisedDelays = AnyRaised();
        MarkChanges();
    }

    private bool AnyRaised()
    {
        return BotWaits.Concat(RelayWaits).Any(delay => delay.IsRaised);
    }

    private void UseDelays(BotDelays delays)
    {
        _delays = delays;
        _changed = true;

        if (_bots is { } bots)
            bots.Delays = delays;

        _preferences?.RememberHost(delays.ToConfiguration());
        HasRaisedDelays = AnyRaised();
        MarkChanges();
    }

    private void AttachBanking(BankingPreferences live)
    {
        // Changed while the host got ready, the tab's settings win; else the bots', which its command line may have set.
        _bankingLive = live;

        if (_bankingChanged)
        {
            UseBanking();

            return;
        }

        var remembered = _remembers;

        _remembers = false;
        BankFullAt = live.FullAt;
        BankUseZaaps = live.UseZaaps;
        _remembers = remembered;
        MarkChanges();
    }

    // An emptied box, or one past its bounds, keeps the share the bots had; the next valid one is theirs at once.
    private void UseBanking()
    {
        if (!_remembers || BankFullAt is not { } share || share < BankFullAtMinimum || share > BankFullAtMaximum)
            return;

        var banking = new BankingPreferences { FullAt = (int)share, UseZaaps = BankUseZaaps };

        _bankingChanged = true;

        if (_bankingLive is { } live)
        {
            live.FullAt = banking.FullAt;
            live.UseZaaps = banking.UseZaaps;
        }

        _preferences?.RememberHost(banking.ToConfiguration());
        MarkChanges();
    }

    private void UseTimeouts(RelayTimeouts timeouts)
    {
        _timeouts = timeouts;
        _preferences?.RememberHost(timeouts.ToConfiguration());
        HasRaisedDelays = AnyRaised();
        MarkChanges();
    }

    partial void OnBankFullAtChanged(decimal? value)
    {
        OnPropertyChanged(nameof(IsBankFullAtChanged));
        UseBanking();
    }

    partial void OnBankUseZaapsChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBankZaapsOff));
        UseBanking();
    }

    partial void OnFilterChanged(string value)
    {
        Search();
    }

    partial void OnRecordsCapturesChanged(bool value)
    {
        if (!_remembers)
            return;

        MarkChanges();

        // On is the relay's default: forgotten, the host's own configuration decides again. Off also forgets the older choice
        // to record every connection, which would go on recording the game's.
        _preferences?.RememberHost(value
            ? new Dictionary<string, string?> { [RelaySettings.GameCapturesEnabledKey] = null }
            : new Dictionary<string, string?> { [RelaySettings.GameCapturesEnabledKey] = bool.FalseString, [RelaySettings.CapturesEnabledKey] = null });
    }

    partial void OnCaptureDaysChanged(decimal? value)
    {
        UseRetention(RelaySettings.CapturesRetentionDaysKey, value, DefaultCaptureDays, CaptureDaysMaximum);
    }

    partial void OnCaptureMegabytesChanged(decimal? value)
    {
        UseRetention(RelaySettings.CapturesRetentionMegabytesKey, value, DefaultCaptureMegabytes, CaptureMegabytesMaximum);
    }

    // An emptied box, or one past its bounds, keeps what the host had; the next valid one is its own at once.
    private void UseRetention(string key, decimal? value, decimal fallback, decimal maximum)
    {
        if (!_remembers || value is not { } limit || limit < CaptureLimitMinimum || limit > maximum || limit != decimal.Truncate(limit))
            return;

        MarkChanges();
        _preferences?.RememberHost(new Dictionary<string, string?> { [key] = limit == fallback ? null : limit.ToString(CultureInfo.InvariantCulture) });
    }

    private string RetentionOf(decimal days, decimal megabytes)
    {
        var size = megabytes.ToString("N0", _localizer.Culture);

        return (days, megabytes) switch
        {
            (0, 0) => _localizer["CapturesKeptForever"],
            (0, _) => _localizer.Format("CapturesKeptBySize", size),
            (_, 0) => _localizer.Format("CapturesKeptByAge", _localizer.Plural("CaptureDays", (long)days)),
            _ => _localizer.Format("CapturesKeptBoth", _localizer.Plural("CaptureDays", (long)days), size),
        };
    }

    [RelayCommand(CanExecute = nameof(HasRaisedDelays))]
    private void RestoreDelays()
    {
        UseDelays(BotDelays.Measured with { Display = _delays.Display });
        UseTimeouts(RelayTimeouts.Default);

        foreach (var delay in BotWaits.Concat(RelayWaits))
            delay.Reread();

        HasRaisedDelays = AnyRaised();
        MarkChanges();
        _toasts.Show(new Toast(_localizer["DelaysRestored"], Tone.Success));
    }

    [RelayCommand(CanExecute = nameof(IsBankFullAtChanged))]
    private void ResetBankFullAt()
    {
        BankFullAt = BankingPreferences.DefaultFullAt;
    }

    [RelayCommand(CanExecute = nameof(IsBankZaapsOff))]
    private void ResetBankZaaps()
    {
        BankUseZaaps = true;
    }

    [RelayCommand(CanExecute = nameof(IsCapturesOff))]
    private void ResetCaptures()
    {
        RecordsCaptures = true;
    }

    [RelayCommand(CanExecute = nameof(IsCaptureDaysChanged))]
    private void ResetCaptureDays()
    {
        CaptureDays = DefaultCaptureDays;
    }

    [RelayCommand(CanExecute = nameof(IsCaptureMegabytesChanged))]
    private void ResetCaptureMegabytes()
    {
        CaptureMegabytes = DefaultCaptureMegabytes;
    }

    [RelayCommand]
    private void Choose(SettingsSection section)
    {
        Select(Sections[(int)section]);
    }

    [RelayCommand]
    private void OpenPath(string path)
    {
        // A file is shown in its folder: the system's file manager opens folders.
        var folder = File.Exists(path) || Path.HasExtension(path) ? Path.GetDirectoryName(path) : path;

        if (!string.IsNullOrEmpty(folder))
            OpenFolder?.Invoke(folder);
    }

    [RelayCommand]
    private void OpenLicences()
    {
        OpenFile?.Invoke(LicencesPath);
    }

    [RelayCommand]
    private void ResetLayoutToDefault()
    {
        ResetLayout?.Invoke();
    }

    [RelayCommand]
    private void UseFrench()
    {
        _useLanguage(Localizer.French);
    }

    [RelayCommand]
    private void UseEnglish()
    {
        _useLanguage(Localizer.English);
    }
}
