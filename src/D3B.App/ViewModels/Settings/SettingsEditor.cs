using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>Edits an activity's settings apart: a field per setting, by section, each checked as the user types (D98).</summary>
/// <remarks>
/// The fields write into a copy of the settings, which the profile saves once they hold. Each change checks them all
/// again: what is wrong with each, which show, the rules the activity declared, and what a setting sought within another
/// offers.
/// </remarks>
internal sealed partial class SettingsEditor : ObservableObject
{
    private readonly List<SettingFieldViewModel> _fields = [];

    /// <summary>Gets the settings the activity declares.</summary>
    public ActivitySettings Settings { get; }

    /// <summary>Gets the settings being edited, a copy, or <see langword="null"/> for an activity with none.</summary>
    public object? Draft { get; }

    /// <summary>Gets the game's values the settings name, or <see langword="null"/> when the host has none.</summary>
    public GameReferences? Game { get; }

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the settings, by section.</summary>
    public IReadOnlyList<SettingSectionViewModel> Sections { get; }

    /// <summary>Gets whether the activity declares no setting.</summary>
    public bool IsEmpty => Settings.IsEmpty;

    /// <summary>Gets what the activity's rules say, those that do not hold, a line each.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRuleProblems))]
    public partial string Rules { get; private set; } = "";

    /// <summary>Gets whether something is wrong with the settings.</summary>
    [ObservableProperty]
    public partial bool HasProblems { get; private set; }

    /// <summary>Gets whether a rule of the activity's does not hold.</summary>
    public bool HasRuleProblems => Rules.Length > 0;

    private GameNames Names { get; }

    /// <summary>Initializes a new instance of the <see cref="SettingsEditor"/> class.</summary>
    /// <param name="settings">The settings the activity declares.</param>
    /// <param name="draft">The settings to edit, which the editor writes into.</param>
    /// <param name="game">The game's values the settings name, or <see langword="null"/>.</param>
    /// <param name="names">Names those values.</param>
    /// <param name="localizer">The app's texts.</param>
    public SettingsEditor(ActivitySettings settings, object? draft, GameReferences? game, GameNames names, Localizer localizer)
    {
        Settings = settings;
        Draft = draft;
        Game = game;
        Names = names;
        Localizer = localizer;

        var sections = new List<SettingSectionViewModel>();
        var fields = new List<SettingFieldViewModel>();
        var title = settings.All.Count > 0 ? settings.All[0].Section : null;

        foreach (var setting in settings.All)
        {
            if (setting.Section != title && fields.Count > 0)
            {
                sections.Add(new SettingSectionViewModel(title, fields, this));
                fields = [];
            }

            title = setting.Section;

            var field = Create(setting);

            _fields.Add(field);
            fields.Add(field);
        }

        if (fields.Count > 0)
            sections.Add(new SettingSectionViewModel(title, fields, this));

        Sections = sections;
        Changed();
    }

    /// <summary>Checks the settings again, once a field changed.</summary>
    public void Changed()
    {
        if (Draft is null)
            return;

        var problems = Settings.Check(Draft, Game);
        var culture = Localizer.Culture;

        foreach (var field in _fields)
            field.Update(problems.FirstOrDefault(problem => problem.Setting == field.Definition));

        foreach (var section in Sections)
            section.Update(this);

        Rules = string.Join(Environment.NewLine, problems.Where(problem => problem.Setting is null).Select(problem => problem.Message?.In(culture)));
        HasProblems = problems.Count > 0 || _fields.Any(field => field.IsVisible && field.HasInputProblem);
    }

    /// <summary>Names everything again, in the app's language.</summary>
    public void Relabel()
    {
        foreach (var field in _fields)
            field.Relabel();

        Changed();
    }

    /// <summary>Writes the settings, to store them.</summary>
    /// <returns>The settings, as JSON.</returns>
    public string Write()
    {
        return Settings.Write(Draft);
    }

    /// <summary>Says what is wrong with a setting, in the app's language.</summary>
    /// <param name="problem">What is wrong.</param>
    /// <returns>The text.</returns>
    public string ProblemText(SettingProblem problem)
    {
        var culture = Localizer.Culture;
        var setting = problem.Setting;

        return problem.Kind switch
        {
            SettingProblemKind.Required => Localizer["SettingRequired"],
            SettingProblemKind.OutOfRange => Localizer.Format("SettingOutOfRange", setting?.Minimum, setting?.Maximum),
            SettingProblemKind.TooLong => Localizer.Format("SettingTooLong", setting?.MaximumLength),
            SettingProblemKind.Unknown => Localizer["SettingUnknown"],
            SettingProblemKind.Repeated => Localizer["SettingRepeated"],
            SettingProblemKind.NotWithin => Localizer.Format("SettingNotWithin", setting?.Within?.Name.In(culture)),
            _ => problem.Message?.In(culture) ?? "",
        };
    }

    /// <summary>Gets the value of the setting another is sought within.</summary>
    /// <param name="setting">The setting sought within another.</param>
    /// <returns>The other's value, or <see langword="null"/> when it has none, or the setting is sought within nothing.</returns>
    public int? WithinOf(SettingDefinition setting)
    {
        return Draft is not null && setting.Within?.GetValue(Draft) is int within ? within : null;
    }

    /// <summary>Gets the values of the game a setting offers, named in the app's language.</summary>
    /// <param name="setting">A setting of the game's values.</param>
    /// <param name="within">The value of the setting it is sought within, if any.</param>
    /// <returns>A profession's resources by the level it gathers them from; every other list by name.</returns>
    public IReadOnlyList<OptionViewModel> OptionsOf(SettingDefinition setting, int? within)
    {
        if (Game is null)
            return [];

        var options = Game.Options(setting.Kind, within).Select(reference => new OptionViewModel(reference.Id, Names.Name(setting.Kind, reference), Names.Detail(reference)));

        if (setting.Kind is SettingKind.Resource && within is not null)
            return [.. options];

        var compare = StringComparer.Create(Localizer.Culture, CompareOptions.IgnoreCase);

        return [.. options.OrderBy(option => option.Name, compare)];
    }

    /// <summary>Names a value of the game a setting holds, whether the game offers it or not.</summary>
    /// <param name="setting">The setting.</param>
    /// <param name="id">The value's id.</param>
    /// <returns>It, named.</returns>
    public OptionViewModel OptionOf(SettingDefinition setting, int id)
    {
        var reference = Game?.Options(setting.Kind).FirstOrDefault(option => option.Id == id) ?? new GameReference(id, 0, null);

        return new OptionViewModel(id, Names.Name(setting.Kind, reference), Names.Detail(reference));
    }

    private SettingFieldViewModel Create(SettingDefinition setting)
    {
        return (setting.Kind, setting.IsList) switch
        {
            (SettingKind.Flag, _) => new FlagFieldViewModel(setting, this),
            (SettingKind.Number, _) => new NumberFieldViewModel(setting, this),
            (SettingKind.Text, _) => new TextFieldViewModel(setting, this),
            (SettingKind.Choice, false) => new ChoiceFieldViewModel(setting, this),
            (SettingKind.Choice, true) => new ChoicesFieldViewModel(setting, this),
            (_, false) => new GameFieldViewModel(setting, this),
            _ => new GamesFieldViewModel(setting, this),
        };
    }
}
