using CommunityToolkit.Mvvm.ComponentModel;

namespace D3B.App.ViewModels.Settings;

/// <summary>A resource the dialog offers: its name and icon, its profession and level, checked or not.</summary>
internal sealed partial class ResourceChoiceViewModel : ObservableObject
{
    private readonly Action<ResourceChoiceViewModel> _checked;

    /// <summary>Gets the resource.</summary>
    public OptionViewModel Option { get; }

    /// <summary>Gets the profession that gathers it, or <see langword="null"/> for one of <c>Base</c>'s (D150).</summary>
    public int? JobId { get; }

    /// <summary>Gets the level its profession gathers it from, if it has one.</summary>
    public int? Level { get; }

    /// <summary>Gets whether a character playing the profile cannot gather it yet, as its bot last saw it.</summary>
    public bool IsTooHigh { get; }

    /// <summary>Gets its level, said, or nothing for one of <c>Base</c>'s.</summary>
    public string LevelText => Level?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? "";

    /// <summary>Gets or sets whether it is checked, to be added.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    /// <summary>Initializes a new instance of the <see cref="ResourceChoiceViewModel"/> class.</summary>
    /// <param name="option">The resource.</param>
    /// <param name="jobId">The profession that gathers it, or <see langword="null"/>.</param>
    /// <param name="level">The level it is gathered from, if any.</param>
    /// <param name="tooHigh">Whether a character playing the profile cannot gather it yet.</param>
    /// <param name="checked">Told when it is checked or unchecked.</param>
    public ResourceChoiceViewModel(OptionViewModel option, int? jobId, int? level, bool tooHigh, Action<ResourceChoiceViewModel> @checked)
    {
        Option = option;
        JobId = jobId;
        Level = level;
        IsTooHigh = tooHigh;
        _checked = @checked;
    }

    partial void OnIsCheckedChanged(bool value)
    {
        _checked(this);
    }
}
