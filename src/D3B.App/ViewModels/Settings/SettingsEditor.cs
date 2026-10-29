using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>Gets what the settings lead to, as the activity reckons it from the pack, if it says (D127).</summary>
    public SettingsPreview? Preview { get; private set; }

    /// <summary>Gets the characters that play the profile, with their professions' levels, as their bots last saw them.</summary>
    public IReadOnlyList<PlayerLevels> Players { get; }

    /// <summary>Gets what says the activity's texts in the app's language, if the host runs.</summary>
    public GameTextFormatter? Says { get; }

    /// <summary>Gets the pack's atlas, which places the zone's maps, if the host runs.</summary>
    public WorldAtlas? Atlas { get; }

    private GameNames Names { get; }

    private GameIcons? Icons { get; }

    private IServiceProvider? Services { get; }

    /// <summary>Initializes a new instance of the <see cref="SettingsEditor"/> class.</summary>
    /// <param name="settings">The settings the activity declares.</param>
    /// <param name="draft">The settings to edit, which the editor writes into.</param>
    /// <param name="game">The game's values the settings name, or <see langword="null"/>.</param>
    /// <param name="names">Names those values.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="services">The host's services, which the activity reckons what its settings lead to from, if it runs.</param>
    /// <param name="players">The characters that play the profile, with their professions' levels.</param>
    public SettingsEditor(ActivitySettings settings, object? draft, GameReferences? game, GameNames names, Localizer localizer, IServiceProvider? services = null, IReadOnlyList<PlayerLevels>? players = null)
    {
        Settings = settings;
        Draft = draft;
        Game = game;
        Names = names;
        Localizer = localizer;
        Services = services;
        Players = players ?? [];
        Atlas = services?.GetService<GameWorld>()?.Atlas;
        Icons = GameIcons.Of(services?.GetService<GameTables>());
        Says = services is null ? null : new GameTextFormatter(localizer, services.GetService<GameWorld>(), services.GetService<GameTexts>(), services.GetService<GameTables>());

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

        // Before the fields, which show what it says.
        Preview = Services is null ? null : Settings.Preview(Draft, Services);

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

    /// <summary>Gets the value of a setting another's suggestions follow, such as the resources whose sub-areas a sub-area setting offers first.</summary>
    /// <param name="setting">The setting suggested by another.</param>
    /// <returns>The other's values, those it shows; every resource the game has when it is hidden, as when the activity harvests everything; none when the setting is suggested by nothing.</returns>
    public IReadOnlyList<int> SuggestionsOf(SettingDefinition setting)
    {
        if (Draft is null || setting.SuggestedBy is not { } by)
            return [];

        if (!Settings.IsVisible(by, Draft))
            return Game is null ? [] : [.. Game.Options(SettingKind.Resource).Select(resource => resource.Id)];

        return [.. by.GetValues(Draft).Cast<int>()];
    }

    /// <summary>Says who gathers a resource, beside its name: its profession and level, or for one of <c>Base</c>'s where the learned graphics place it (D150).</summary>
    /// <param name="resource">The resource's item.</param>
    /// <returns>Its profession and level, or where it was seen; <see langword="null"/> when the game names no such resource.</returns>
    public string? SourceOf(int resource)
    {
        if (Game is null)
            return null;

        if (Game.TryGetSource(resource, out var source))
            return Localizer.Format("ResourceSource", Names.Name(SettingKind.Job, JobOf(source.JobId)), source.Level);

        if (!Game.IsBase(resource))
            return null;

        return Game.PlacesOf(resource) is { Count: > 0 } places ? Localizer.Format("ResourceBasePlaced", places.Count) : Localizer["ResourceBaseUnseen"];
    }

    /// <summary>Says which character playing the profile lacks a profession's level for a resource, if one does.</summary>
    /// <param name="resource">The resource's item.</param>
    /// <returns>The first such character, the level asked and its own; <see langword="null"/> when every one has it, or none was seen.</returns>
    public string? MissingLevelOf(int resource)
    {
        if (Game is null || !Game.TryGetSource(resource, out var source))
            return null;

        foreach (var player in Players)
        {
            // A profession a bot has not seen counts as the first level, as the activity counts it.
            var level = player.JobLevels.TryGetValue(source.JobId, out var known) && known > 0 ? known : 1;

            if (player.JobLevels.Count > 0 && level < source.Level)
                return Localizer.Format("ResourceMissingLevel", Names.Name(SettingKind.Job, JobOf(source.JobId)), source.Level, player.Name, level);
        }

        return null;
    }

    /// <summary>Names a value of the game, in the app's language.</summary>
    /// <param name="kind">Its kind.</param>
    /// <param name="reference">The value.</param>
    /// <returns>Its name.</returns>
    public string NameOf(SettingKind kind, GameReference reference)
    {
        return Names.Name(kind, reference);
    }

    /// <summary>Gets the values of the game a setting offers, named in the app's language.</summary>
    /// <param name="setting">A setting of the game's values.</param>
    /// <param name="within">The value of the setting it is sought within, if any.</param>
    /// <returns>A profession's resources by the level it gathers them from; every other list by name.</returns>
    public IReadOnlyList<OptionViewModel> OptionsOf(SettingDefinition setting, int? within)
    {
        if (Game is null)
            return [];

        var options = Game.Options(setting.Kind, within).Select(reference => Option(setting.Kind, reference));

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

        return Option(setting.Kind, reference);
    }

    private OptionViewModel Option(SettingKind kind, GameReference reference)
    {
        // A resource shows its icon, and its profession's beside the detail naming it; a profession its own.
        var (icon, detailIcon) = kind switch
        {
            SettingKind.Resource => (Icons?.Item(reference.Id), Game is not null && Game.TryGetSource(reference.Id, out var source) ? Icons?.Job(source.JobId) : null),
            SettingKind.Job => (Icons?.Job(reference.Id), null),
            _ => (null, null),
        };

        return new OptionViewModel(reference.Id, Names.Name(kind, reference), DetailOf(kind, reference), icon, detailIcon);
    }

    private GameReference JobOf(int jobId)
    {
        return Game?.Options(SettingKind.Job).FirstOrDefault(job => job.Id == jobId) ?? new GameReference(jobId, 0, null);
    }

    private string? DetailOf(SettingKind kind, GameReference reference)
    {
        // A resource of any profession says whose it is, its level beside.
        return kind is SettingKind.Resource ? SourceOf(reference.Id) ?? Names.Detail(reference) : Names.Detail(reference);
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
            (SettingKind.Resource, true) => new ResourcesFieldViewModel(setting, this),
            (SettingKind.SubArea, true) when setting.SuggestedBy is not null => new SubAreasFieldViewModel(setting, this),
            (_, false) => new GameFieldViewModel(setting, this),
            _ => new GamesFieldViewModel(setting, this),
        };
    }
}
