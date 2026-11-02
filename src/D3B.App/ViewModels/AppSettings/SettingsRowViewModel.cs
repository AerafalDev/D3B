using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;

namespace D3B.App.ViewModels.AppSettings;

/// <summary>A row of the settings' tab that is no delay: its title and the line under it, which the search reads (D329).</summary>
internal sealed partial class SettingsRowViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly string _key;

    /// <summary>Gets what the row is.</summary>
    [ObservableProperty]
    public partial string Label { get; private set; } = "";

    /// <summary>Gets the line under its title, if it has one.</summary>
    [ObservableProperty]
    public partial string Detail { get; private set; } = "";

    /// <summary>Gets or sets whether the search leaves it.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; } = true;

    /// <summary>Gets whether it has a line under its title.</summary>
    public bool HasDetail { get; }

    /// <summary>Initializes a new instance of the <see cref="SettingsRowViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="key">The key of its title, the line under it keyed with <c>Detail</c> after it.</param>
    /// <param name="hasDetail">Whether it has a line under its title.</param>
    public SettingsRowViewModel(Localizer localizer, string key, bool hasDetail = true)
    {
        _localizer = localizer;
        _key = key;
        HasDetail = hasDetail;
        Relabel();
    }

    /// <summary>Says the row again, in the app's language.</summary>
    public void Relabel()
    {
        Label = _localizer[_key];
        Detail = HasDetail ? _localizer[_key + "Detail"] : "";
    }

    /// <summary>Tells whether the row's title or line holds a text.</summary>
    /// <param name="filter">The text, trimmed; empty holds always.</param>
    /// <returns>Whether it holds it.</returns>
    public bool Holds(string filter)
    {
        return filter.Length == 0 || Label.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || Detail.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }
}
