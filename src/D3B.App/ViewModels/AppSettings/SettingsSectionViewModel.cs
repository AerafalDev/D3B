using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;

namespace D3B.App.ViewModels.AppSettings;

/// <summary>A section of the settings' tab, in its column: its name, whether it shows, is chosen, and holds values changed (D329).</summary>
internal sealed partial class SettingsSectionViewModel : ObservableObject
{
    private readonly Localizer _localizer;

    /// <summary>Gets the section.</summary>
    public SettingsSection Section { get; }

    /// <summary>Gets its name.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    /// <summary>Gets the line under its title, saying what it holds.</summary>
    [ObservableProperty]
    public partial string Lead { get; private set; } = "";

    /// <summary>Gets or sets whether it is the section shown.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets or sets whether the search leaves a row of it.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; } = true;

    /// <summary>Gets or sets whether a value of it differs from its default, which its dot says.</summary>
    [ObservableProperty]
    public partial bool HasChanges { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SettingsSectionViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="section">The section.</param>
    public SettingsSectionViewModel(Localizer localizer, SettingsSection section)
    {
        _localizer = localizer;
        Section = section;
        Relabel();
    }

    /// <summary>Says the section again, in the app's language.</summary>
    public void Relabel()
    {
        Title = _localizer["Section" + Section];
        Lead = _localizer["Section" + Section + "Lead"];
    }
}
