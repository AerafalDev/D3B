using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>The dialog choosing resources or monsters several at once: searched by name, filtered by profession or super race, resources its characters cannot gather yet dimmed (D159, D236).</summary>
/// <remarks>The values checked are added in the order they were checked, after those the list holds.</remarks>
internal sealed partial class GamePickerViewModel : ObservableObject
{
    private readonly Dialogs _dialogs;
    private readonly PickedGamesFieldViewModel _field;
    private readonly Localizer _localizer;
    private readonly IReadOnlyList<GameChoiceViewModel> _all;
    private readonly List<GameChoiceViewModel> _checked = [];
    private readonly string _add;

    /// <summary>Gets the groups to filter by, every one first: professions or super races.</summary>
    public IReadOnlyList<FilterChipViewModel> Groups { get; }

    /// <summary>Gets the values shown, by level then name.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<GameChoiceViewModel> Shown { get; private set; } = [];

    /// <summary>Gets or sets what the name searched holds.</summary>
    [ObservableProperty]
    public partial string? Search { get; set; }

    /// <summary>Gets what the button adding them says, with how many are checked.</summary>
    [ObservableProperty]
    public partial string AddText { get; private set; } = "";

    /// <summary>Gets whether a value is checked.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial bool HasChecked { get; private set; }

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets whether no value matches.</summary>
    public bool IsEmpty => Shown.Count == 0;

    private FilterChipViewModel SelectedGroup => Groups.First(group => group.IsSelected);

    /// <summary>Initializes a new instance of the <see cref="GamePickerViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs, which it closes.</param>
    /// <param name="field">The list it adds to.</param>
    /// <param name="editor">The editor the list belongs to.</param>
    /// <param name="offered">The values the list does not hold yet.</param>
    public GamePickerViewModel(Dialogs dialogs, PickedGamesFieldViewModel field, SettingsEditor editor, IReadOnlyList<OptionViewModel> offered)
    {
        _dialogs = dialogs;
        _field = field;
        _localizer = editor.Localizer;
        _add = field.ChooseKey;
        Title = _localizer[_add];

        var kind = field.Definition.Kind;
        var compare = StringComparer.Create(editor.Localizer.Culture, CompareOptions.IgnoreCase);

        _all =
        [
            .. offered
                .Select(option =>
                {
                    var id = (int)option.Value;
                    var (group, level) = editor.GroupOf(kind, id);

                    return new GameChoiceViewModel(option, group, level, kind is SettingKind.Resource && editor.MissingLevelOf(id) is not null, OnChecked);
                })
                .OrderBy(choice => choice.Level ?? 0)
                .ThenBy(choice => choice.Option.Name, compare),
        ];

        var groups = new List<FilterChipViewModel> { new(null, editor.Localizer["AllJobs"], null, Select) { IsSelected = true } };

        foreach (var groupId in _all.Select(choice => choice.GroupId).Distinct().OrderBy(id => id is null).ThenBy(id => id))
        {
            if (groupId is { } id)
            {
                var group = editor.GroupOption(kind, id);

                groups.Add(new FilterChipViewModel(id, group.Name, group.Icon, Select));
            }
            else
            {
                groups.Add(new FilterChipViewModel(null, editor.Localizer[kind is SettingKind.Resource ? "NoJob" : "NoGroup"], null, Select) { IsBase = true });
            }
        }

        Groups = groups;
        AddText = editor.Localizer[_add];
        Filter();
    }

    private void Select(FilterChipViewModel group)
    {
        foreach (var other in Groups)
            other.IsSelected = other == group;

        Filter();
    }

    private void Filter()
    {
        var group = SelectedGroup;
        var search = Search?.Trim() ?? "";

        Shown =
        [
            .. _all.Where(choice => (group.Id is null && !group.IsBase) || (group.IsBase ? choice.GroupId is null : choice.GroupId == group.Id))
                .Where(choice => search.Length == 0 || choice.Option.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)),
        ];
    }

    private void OnChecked(GameChoiceViewModel choice)
    {
        if (choice.IsChecked)
            _checked.Add(choice);
        else
            _checked.Remove(choice);

        HasChecked = _checked.Count > 0;
        AddText = _checked.Count == 0 ? _localizer[_add] : _localizer.Format(_add + "Count", _checked.Count);
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
