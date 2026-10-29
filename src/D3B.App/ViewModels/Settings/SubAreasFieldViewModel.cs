using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Controls;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>A zone's sub-areas: those where the resources asked for grow first, the zone drawn from the atlas, and what the activity says it leads to (D127).</summary>
/// <remarks>
/// The sub-areas chosen come first, in their order, then the others where the resources grow, the most of them first, then
/// the lowest; a click chooses or leaves one, and the box still adds any other. The map and the figures are the
/// activity's preview of its settings, reckoned from the pack as they change.
/// </remarks>
internal sealed partial class SubAreasFieldViewModel : GamesFieldViewModel
{
    private const int MostSuggestions = 8;

    private string? _shown;
    private SettingsPreview? _shownPreview;

    /// <summary>Gets the sub-areas suggested, those chosen first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SuggestionViewModel> Suggestions { get; private set; } = [];

    /// <summary>Gets the zone, as its mini-map draws it, once the activity says what its settings lead to.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasZone))]
    public partial ZoneScene? Zone { get; private set; }

    /// <summary>Gets the figures the activity gives of its settings.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFigures))]
    public partial IReadOnlyList<FigureViewModel> Figures { get; private set; } = [];

    /// <summary>Gets whether a zone is drawn.</summary>
    public bool HasZone => Zone is not null;

    /// <summary>Gets whether the activity gives figures.</summary>
    public bool HasFigures => Figures.Count > 0;

    /// <inheritdoc/>
    public override bool IsWide => true;

    /// <summary>Initializes a new instance of the <see cref="SubAreasFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public SubAreasFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        _shown = null;
        base.Relabel();
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        base.RefreshOptions();
        RefreshSuggestions();
    }

    /// <inheritdoc/>
    protected override void ReadItems()
    {
        base.ReadItems();
        RefreshSuggestions();
    }

    private void RefreshSuggestions()
    {
        if (Editor.Draft is null)
            return;

        var resources = Editor.SuggestionsOf(Definition);
        var within = Editor.WithinOf(Definition);
        var chosen = Definition.GetValues(Draft).Cast<int>().ToList();
        var preview = Editor.Preview;
        var key = $"{string.Join(',', resources)}|{within}|{string.Join(',', chosen)}|{Editor.Localizer.Culture.Name}";

        // Read on every check of the settings: the lists are made again only when what they show changed.
        if (key == _shown && ReferenceEquals(preview, _shownPreview))
            return;

        _shown = key;
        _shownPreview = preview;

        var holding = Editor.Game?.SubAreasHolding(resources, within) ?? [];
        var suggestions = new List<SuggestionViewModel>();

        foreach (var id in chosen)
            suggestions.Add(Suggestion(id, holding.FirstOrDefault(subArea => subArea.SubArea.Id == id), chosen));

        foreach (var subArea in holding.Where(subArea => !chosen.Contains(subArea.SubArea.Id)).Take(Math.Max(0, MostSuggestions - chosen.Count)))
            suggestions.Add(Suggestion(subArea.SubArea.Id, subArea, chosen));

        Suggestions = suggestions;
        Zone = preview is { Zone.Length: > 0 } && Editor.Atlas is { } atlas
            ? ZoneScenes.Of(preview.Zone, atlas, tour: false, preview.LandmarkMapId, Editor.Players.FirstOrDefault(player => player.MapId is not null)?.MapId)
            : null;
        Figures = preview is null || Editor.Says is not { } says ? [] : [.. preview.Figures.Select(figure => new FigureViewModel(says.Say(figure.Value), says.Say(figure.Label)))];
    }

    private SuggestionViewModel Suggestion(int id, SubAreaResources? held, IReadOnlyList<int> chosen)
    {
        var game = Editor.Game;
        var localizer = Editor.Localizer;
        var reference = held?.SubArea ?? game?.Options(SettingKind.SubArea).FirstOrDefault(subArea => subArea.Id == id) ?? new GameReference(id, 0, null);
        var maps = held?.Maps ?? game?.MapsOf(id).Count() ?? 0;
        var names = (held?.Resources ?? []).Select(resource => Editor.NameOf(SettingKind.Resource, game?.Options(SettingKind.Resource).FirstOrDefault(option => option.Id == resource) ?? new GameReference(resource, 0, null)));
        var parts = new List<string>();

        if (reference.Level is { } level)
            parts.Add(localizer.Format("Level", level));

        parts.Add(localizer.Plural("Maps", maps));

        if (held is { Resources.Count: > 0 })
            parts.Add(string.Join(", ", names));

        var place = chosen.ToList().IndexOf(id);

        return new SuggestionViewModel(id, Editor.NameOf(SettingKind.SubArea, reference), string.Join(", ", parts), place < 0 ? null : place, Toggle);
    }
}
