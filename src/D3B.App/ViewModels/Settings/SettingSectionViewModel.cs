using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Localization;

namespace D3B.App.ViewModels.Settings;

/// <summary>A category of a profile's settings: those the activity groups under a title, its rare ones folded under "Advanced" (D415).</summary>
internal sealed partial class SettingSectionViewModel : ObservableObject
{
    private readonly LocalizedText? _title;

    /// <summary>Gets all of the section's settings, in the order the activity declared them.</summary>
    public IReadOnlyList<SettingFieldViewModel> Fields { get; }

    /// <summary>Gets the settings the form shows first: those the activity did not mark rare.</summary>
    public IReadOnlyList<SettingFieldViewModel> InView { get; }

    /// <summary>Gets the rare settings, which the form folds under "Advanced".</summary>
    public IReadOnlyList<SettingFieldViewModel> Advanced { get; }

    /// <summary>Gets the section's title, if it has one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitle))]
    public partial string? Title { get; private set; }

    /// <summary>Gets whether any of its settings shows.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; } = true;

    /// <summary>Gets whether one of its settings differs from the behavior's, which the sections' bar marks (D330).</summary>
    [ObservableProperty]
    public partial bool HasChanges { get; private set; }

    /// <summary>Gets whether a rare setting shows at all, so that the category has a fold.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAdvanced))]
    public partial bool HasAdvanced { get; private set; }

    /// <summary>Gets how many rare settings that show differ from the behavior's, which the fold says and its dot marks.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdvancedChanges))]
    public partial int AdvancedChanged { get; private set; }

    /// <summary>Gets what the fold says: "Advanced", and how many of its settings differ from the behavior's.</summary>
    [ObservableProperty]
    public partial string AdvancedText { get; private set; } = "";

    /// <summary>Gets or sets whether the hand opened the fold.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAdvanced))]
    public partial bool IsAdvancedOpen { get; set; }

    /// <summary>Gets whether a rare setting has a problem, which opens the fold by itself so that none is hidden.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAdvanced))]
    public partial bool HasAdvancedProblem { get; private set; }

    /// <summary>Gets whether a rare setting differs from the behavior's.</summary>
    public bool HasAdvancedChanges => AdvancedChanged > 0;

    /// <summary>Gets whether the rare settings show: the fold is open, or one of them has a problem.</summary>
    public bool ShowsAdvanced => HasAdvanced && (IsAdvancedOpen || HasAdvancedProblem);

    /// <summary>Gets whether the section has a title.</summary>
    public bool HasTitle => Title is not null;

    /// <summary>Initializes a new instance of the <see cref="SettingSectionViewModel"/> class.</summary>
    /// <param name="title">The title the activity gave it.</param>
    /// <param name="fields">Its settings.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public SettingSectionViewModel(LocalizedText? title, IReadOnlyList<SettingFieldViewModel> fields, SettingsEditor editor)
    {
        _title = title;
        Fields = fields;
        InView = [.. fields.Where(field => !field.Definition.IsAdvanced)];
        Advanced = [.. fields.Where(field => field.Definition.IsAdvanced)];
        Title = title?.In(editor.Localizer.Culture);
        AdvancedText = editor.Localizer["ProfileAdvanced"];
    }

    /// <summary>Shows the section as its settings show.</summary>
    /// <param name="editor">The editor it belongs to.</param>
    public void Update(SettingsEditor editor)
    {
        Title = _title?.In(editor.Localizer.Culture);
        IsVisible = Fields.Any(field => field.IsVisible);
        HasChanges = Fields.Any(field => field.IsVisible && field.IsChanged);

        // A rare setting that does not show is not counted, as the editor's own count leaves it out.
        var shown = Advanced.Where(field => field.IsVisible).ToList();

        HasAdvanced = shown.Count > 0;
        AdvancedChanged = shown.Count(field => field.IsChanged);
        HasAdvancedProblem = shown.Any(field => field.HasProblem || field.HasInputProblem);
        AdvancedText = AdvancedChanged > 0 ? editor.Localizer.Plural("ProfileAdvancedChanged", AdvancedChanged) : editor.Localizer["ProfileAdvanced"];
    }

    [RelayCommand]
    private void ToggleAdvanced()
    {
        IsAdvancedOpen = !IsAdvancedOpen;
    }
}
