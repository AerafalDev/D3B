using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;

namespace D3B.App.ViewModels.Studio;

/// <summary>The status bar, VS Code's and next's: the app's state on the left, its context on the right, each item opening what it names (D531, D541).</summary>
/// <remarks>
/// On the left, the app's name and version after its one accent, then the relay and the characters it carries into the game;
/// on the right, the client build and the data pack's version, the bots playing, the problems, whether everything is saved, and
/// the update downloaded. A tooltip says only what the bar does not, as next's hovers.
/// </remarks>
internal sealed partial class StatusBarViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly string _version;
    private StatusFacts _facts = new() { Host = HostStatus.Starting };

    /// <summary>Gets the app's name and version, after the bar's blue dot.</summary>
    [ObservableProperty]
    public partial string VersionText { get; private set; } = "";

    /// <summary>Gets where the relay stands and how many characters it carries into the game.</summary>
    [ObservableProperty]
    public partial string RelayText { get; private set; } = "";

    /// <summary>Gets what the relay's item does not say: why it stopped, what quitting ends, when to start Dofus.</summary>
    [ObservableProperty]
    public partial string RelayTip { get; private set; } = "";

    /// <summary>Gets the tone of the relay's dot.</summary>
    [ObservableProperty]
    public partial Tone RelayTone { get; private set; }

    /// <summary>Gets whether the relay stopped, the item then red.</summary>
    [ObservableProperty]
    public partial bool IsRelayFailed { get; private set; }

    /// <summary>Gets the client build and, once the host opened it, the game data pack's version.</summary>
    [ObservableProperty]
    public partial string ClientText { get; private set; } = "";

    /// <summary>Gets what made the data pack, once the host opened it.</summary>
    [ObservableProperty]
    public partial string ClientTip { get; private set; } = "";

    /// <summary>Gets how many bots play, a figure beside the play icon.</summary>
    [ObservableProperty]
    public partial string PlayingText { get; private set; } = "";

    /// <summary>Gets how many bots play of those connected, in words.</summary>
    [ObservableProperty]
    public partial string PlayingTip { get; private set; } = "";

    /// <summary>Gets whether everything is saved, in words.</summary>
    [ObservableProperty]
    public partial string SavedTip { get; private set; } = "";

    /// <summary>Gets whether the bots playing show: once the host runs.</summary>
    [ObservableProperty]
    public partial bool ShowsPlaying { get; private set; }

    /// <summary>Gets how many problems are errors.</summary>
    [ObservableProperty]
    public partial string ErrorsText { get; private set; } = "0";

    /// <summary>Gets how many problems are warnings.</summary>
    [ObservableProperty]
    public partial string WarningsText { get; private set; } = "0";

    /// <summary>Gets whether any problem is counted.</summary>
    [ObservableProperty]
    public partial bool HasProblems { get; private set; }

    /// <summary>Gets whether every editor is saved.</summary>
    [ObservableProperty]
    public partial bool IsSaved { get; private set; } = true;

    /// <summary>Gets whether an update is downloaded, which a restart installs.</summary>
    [ObservableProperty]
    public partial bool IsUpdateReady { get; private set; }

    /// <summary>Gets the offer to restart into the update.</summary>
    [ObservableProperty]
    public partial string UpdateText { get; private set; } = "";

    /// <summary>Gets or sets what the version opens: the settings' About.</summary>
    public Action? ShowAbout { get; set; }

    /// <summary>Gets or sets what the relay opens: the journal, or the problems once it stopped.</summary>
    public Action? ShowRelay { get; set; }

    /// <summary>Gets or sets what the client and its data open: the settings' Files.</summary>
    public Action? ShowData { get; set; }

    /// <summary>Gets or sets what the bots playing open: the Characters view.</summary>
    public Action? ShowCharacters { get; set; }

    /// <summary>Gets or sets what the problems open: their tab of the bottom panel.</summary>
    public Action? ShowProblems { get; set; }

    /// <summary>Gets or sets what the saving item does: saves everything.</summary>
    public Action? SaveAll { get; set; }

    /// <summary>Gets or sets what the update's offer does: restarts into it.</summary>
    public Action? Restart { get; set; }

    /// <summary>Initializes a new instance of the <see cref="StatusBarViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="version">The app's version.</param>
    public StatusBarViewModel(Localizer localizer, string version)
    {
        _localizer = localizer;
        _version = version;
        Refresh(_facts);
    }

    /// <summary>Says the facts read, in the language chosen.</summary>
    /// <param name="facts">What the studio read.</param>
    public void Refresh(StatusFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        _facts = facts;
        VersionText = $"D3B {_version}";

        var running = facts.Host is HostStatus.Running;

        (RelayText, RelayTone) = facts.Host switch
        {
            HostStatus.Starting => (_localizer["RelayStarting"], Tone.Info),
            HostStatus.Running => (_localizer.Format("RelayReady", facts.InGame), Tone.Success),
            HostStatus.Failed => (_localizer["RelayFailed"], Tone.Danger),
            _ => (_localizer["RelayStopped"], Tone.Neutral),
        };
        IsRelayFailed = facts.Host is HostStatus.Failed;

        var tip = new List<string>();

        if (IsRelayFailed && facts.Failure is { Length: > 0 } failure)
            tip.Add(failure);

        if (running && facts.InGame > 0)
            tip.Add(_localizer["RelayQuitEnds"]);

        if (running)
            tip.Add(_localizer["RelayStartAfter"]);

        RelayTip = string.Join('\n', tip);

        // The pack's version once the host opened it: before, only the build the app ships is known.
        ClientText = facts.ClientBuild;
        ClientTip = facts.PackFormat is { } format ? _localizer.Format("StatusClientDataTip", facts.ClientBuild, format, facts.PackExtractor ?? "?") : _localizer.Format("StatusClientTip", facts.ClientBuild);
        ShowsPlaying = running;
        PlayingText = facts.Playing.ToString(_localizer.Culture);
        PlayingTip = _localizer.Format("StatusPlayingTip", _localizer.Plural("StatusPlaying", facts.Playing), facts.InGame);
        // A host that failed counts its failure, a problem with its fix; one starting has read nothing yet.
        var counted = facts.Host is not HostStatus.Starting;

        ErrorsText = (counted ? facts.Errors : 0).ToString(_localizer.Culture);
        WarningsText = (counted ? facts.Warnings : 0).ToString(_localizer.Culture);
        HasProblems = counted && facts.Errors + facts.Warnings > 0;
        IsSaved = facts.Saved;
        SavedTip = _localizer[facts.Saved ? "StatusSaved" : "StatusUnsavedTip"];
        IsUpdateReady = facts.UpdateVersion is not null;
        UpdateText = facts.UpdateVersion is { } update ? _localizer.Format("StatusRestartTo", update) : "";
    }

    /// <summary>Says the facts read last again, in the language chosen.</summary>
    public void Retell()
    {
        Refresh(_facts);
    }

    [RelayCommand]
    private void ShowAboutPage()
    {
        ShowAbout?.Invoke();
    }

    [RelayCommand]
    private void ShowRelayJournal()
    {
        // Stopped, the relay leads to why and its fix, which the problems hold, not to an empty journal.
        if (IsRelayFailed)
            ShowProblems?.Invoke();
        else
            ShowRelay?.Invoke();
    }

    [RelayCommand]
    private void ShowDataFiles()
    {
        ShowData?.Invoke();
    }

    [RelayCommand]
    private void ShowCharactersView()
    {
        ShowCharacters?.Invoke();
    }

    [RelayCommand]
    private void ShowProblemsTab()
    {
        ShowProblems?.Invoke();
    }

    [RelayCommand]
    private void SaveEverything()
    {
        SaveAll?.Invoke();
    }

    [RelayCommand]
    private void RestartIntoUpdate()
    {
        Restart?.Invoke();
    }
}
