using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Hosting;
using D3B.App.Localization;

namespace D3B.App.ViewModels.Home;

/// <summary>The notice of the home page that says the game sessions are recorded on this machine, until the player has read it (D409).</summary>
/// <remarks>
/// A bug report is only as good as the capture that comes with it, so the game connection is recorded by default. The
/// player is told once, on the first start after the change, what is kept, where, for how long, that nothing leaves the
/// machine unless they send a report, and how to turn it off: the Network settings say the same all the time.
/// </remarks>
internal sealed partial class CapturesNoticeViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly SettingsViewModel _settings;
    private readonly AppPreferences? _preferences;

    /// <summary>Gets whether the notice shows: the sessions are recorded and the player has not read it.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; private set; }

    /// <summary>Gets what the notice says, in the app's language and with the retention now set.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = "";

    /// <summary>Gets or sets what shows the Network settings.</summary>
    public Action? SettingsRequested { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CapturesNoticeViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="settings">The settings, which say whether the sessions are recorded and for how long they are kept.</param>
    /// <param name="preferences">What the app remembers, or <see langword="null"/> to remember nothing.</param>
    public CapturesNoticeViewModel(Localizer localizer, SettingsViewModel settings, AppPreferences? preferences)
    {
        _localizer = localizer;
        _settings = settings;
        _preferences = preferences;
    }

    /// <summary>Reads the settings again, and says the notice in the language chosen.</summary>
    public void Refresh()
    {
        IsShown = _preferences is { CapturesNoticeSeen: false } && _settings.RecordsCaptures;
        Text = _localizer.Format("CapturesNoticeText", _settings.RetentionText);
    }

    [RelayCommand]
    private void Understand()
    {
        _preferences?.RememberCapturesNoticeSeen();
        IsShown = false;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke();
    }
}
