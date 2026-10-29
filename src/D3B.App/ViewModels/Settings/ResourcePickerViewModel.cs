using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>The dialog choosing resources several at once: searched by name, filtered by profession, those its characters cannot gather yet dimmed (D159).</summary>
/// <remarks>The resources checked are added in the order they were checked, after those the list holds.</remarks>
internal sealed partial class ResourcePickerViewModel : ObservableObject
{
    private readonly Dialogs _dialogs;
    private readonly ResourcesFieldViewModel _field;
    private readonly Localizer _localizer;
    private readonly IReadOnlyList<ResourceChoiceViewModel> _all;
    private readonly List<ResourceChoiceViewModel> _checked = [];

    /// <summary>Gets the professions to filter by, every one first.</summary>
    public IReadOnlyList<JobFilterViewModel> Jobs { get; }

    /// <summary>Gets the resources shown, by level then name.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<ResourceChoiceViewModel> Shown { get; private set; } = [];

    /// <summary>Gets or sets what the name searched holds.</summary>
    [ObservableProperty]
    public partial string? Search { get; set; }

    /// <summary>Gets what the button adding them says, with how many are checked.</summary>
    [ObservableProperty]
    public partial string AddText { get; private set; } = "";

    /// <summary>Gets whether a resource is checked.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial bool HasChecked { get; private set; }

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets whether no resource matches.</summary>
    public bool IsEmpty => Shown.Count == 0;

    private JobFilterViewModel SelectedJob => Jobs.First(job => job.IsSelected);

    /// <summary>Initializes a new instance of the <see cref="ResourcePickerViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs, which it closes.</param>
    /// <param name="field">The list it adds to.</param>
    /// <param name="editor">The editor the list belongs to.</param>
    /// <param name="offered">The resources the list does not hold yet.</param>
    public ResourcePickerViewModel(Dialogs dialogs, ResourcesFieldViewModel field, SettingsEditor editor, IReadOnlyList<OptionViewModel> offered)
    {
        _dialogs = dialogs;
        _field = field;
        _localizer = editor.Localizer;
        Title = _localizer["AddResources"];

        var game = editor.Game;
        var compare = StringComparer.Create(editor.Localizer.Culture, CompareOptions.IgnoreCase);

        _all =
        [
            .. offered
                .Select(option =>
                {
                    var id = (int)option.Value;
                    ResourceSource? source = game is not null && game.TryGetSource(id, out var found) ? found : null;

                    return new ResourceChoiceViewModel(option, source?.JobId, source?.Level, editor.MissingLevelOf(id) is not null, OnChecked);
                })
                .OrderBy(choice => choice.Level ?? 0)
                .ThenBy(choice => choice.Option.Name, compare),
        ];

        var jobs = new List<JobFilterViewModel> { new(null, editor.Localizer["AllJobs"], null, Select) { IsSelected = true } };

        foreach (var jobId in _all.Select(choice => choice.JobId).Distinct().OrderBy(id => id is null))
        {
            if (jobId is { } id)
            {
                var job = editor.JobOption(id);

                jobs.Add(new JobFilterViewModel(id, job.Name, job.Icon, Select));
            }
            else
            {
                jobs.Add(new JobFilterViewModel(null, editor.Localizer["NoJob"], null, Select) { IsBase = true });
            }
        }

        Jobs = jobs;
        AddText = editor.Localizer["AddResources"];
        Filter();
    }

    private void Select(JobFilterViewModel job)
    {
        foreach (var other in Jobs)
            other.IsSelected = other == job;

        Filter();
    }

    private void Filter()
    {
        var job = SelectedJob;
        var search = Search?.Trim() ?? "";

        Shown =
        [
            .. _all.Where(choice => (job.Id is null && !job.IsBase) || (job.IsBase ? choice.JobId is null : choice.JobId == job.Id))
                .Where(choice => search.Length == 0 || choice.Option.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)),
        ];
    }

    private void OnChecked(ResourceChoiceViewModel choice)
    {
        if (choice.IsChecked)
            _checked.Add(choice);
        else
            _checked.Remove(choice);

        HasChecked = _checked.Count > 0;
        AddText = _checked.Count == 0 ? _localizer["AddResources"] : _localizer.Format("AddResourcesCount", _checked.Count);
    }

    [RelayCommand(CanExecute = nameof(HasChecked))]
    private void Add()
    {
        _dialogs.Close(this);
        _field.AddAll([.. _checked.Select(choice => choice.Option)]);
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close(this);
    }

    partial void OnSearchChanged(string? value)
    {
        Filter();
    }
}
