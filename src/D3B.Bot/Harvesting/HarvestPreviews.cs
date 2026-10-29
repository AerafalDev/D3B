using D3B.Bot.Interactives;
using D3B.Bot.Localization;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Criteria;
using D3B.Engine.World;

namespace D3B.Bot.Harvesting;

/// <summary>What harvesting settings lead to, for the app's profile editor: the zone's maps, its resources, its nearest bank (D127).</summary>
/// <remarks>
/// One per host. The zone's maps are those a tour goes through, the probable ones apart, from what the host's bots saw and
/// learned of the graphics (D138). The path to the nearest bank is found from each bank's front to the zone, a door more
/// into the bank, for a character of whom nothing is known: criteria not met, no zaap. The app asks as the settings
/// change: the last preview is kept for the same zone and resources, while the bots have learned nothing more.
/// </remarks>
public sealed class HarvestPreviews
{
    private readonly GameWorld _world;
    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly HarvestSightings _sightings;
    private readonly GraphicSkills _graphics;
    private readonly Lazy<WorldPathfinder> _pathfinder;
    private readonly Lock _lock = new();
    private string? _key;
    private SettingsPreview? _last;

    /// <summary>Initializes a new instance of the <see cref="HarvestPreviews"/> class.</summary>
    /// <param name="world">The installed build's world.</param>
    /// <param name="tables">The installed build's tables: the professions' skills and the banks.</param>
    /// <param name="maps">The installed build's maps: the graphics of the zone's elements.</param>
    /// <param name="sightings">What the host's bots saw of the maps.</param>
    /// <param name="graphics">What the host learned of the maps' graphics.</param>
    public HarvestPreviews(GameWorld world, GameTables tables, GameMaps maps, HarvestSightings sightings, GraphicSkills graphics)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(sightings);
        ArgumentNullException.ThrowIfNull(graphics);

        _world = world;
        _tables = tables;
        _maps = maps;
        _sightings = sightings;
        _graphics = graphics;
        _pathfinder = new Lazy<WorldPathfinder>(() => new WorldPathfinder(world.Graph, world.Atlas), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private string KeyOf(HarvestSettings settings)
    {
        // What the bots saw and learned only grows: their counts tell a preview reckoned before.
        return $"{settings.EverythingKnown}|{string.Join(',', settings.Resources)}|{settings.Area}|{string.Join(',', settings.SubAreas)}|{_sightings.Count}|{_graphics.Count}";
    }

    private static bool TryGetVertex(WorldGraph graph, long mapId, out int vertex)
    {
        for (var zone = 1; zone <= WorldVertex.MaxZone; zone++)
        {
            if (graph.TryGetVertex(mapId, zone, out vertex))
                return true;
        }

        vertex = -1;

        return false;
    }

    /// <summary>Reckons what settings lead to.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The preview, or nothing while the settings name no resource or no place.</returns>
    public SettingsPreview? Of(HarvestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var key = KeyOf(settings);

        lock (_lock)
        {
            if (key == _key)
                return _last;
        }

        var preview = Reckon(settings);

        lock (_lock)
        {
            (_key, _last) = (key, preview);
        }

        return preview;
    }

    private SettingsPreview? Reckon(HarvestSettings settings)
    {
        var atlas = _world.Atlas;
        var zone = HarvestZone.Of(settings, atlas, _world.Graph, _tables.Jobs, _maps);

        if (zone.Targets.Count is 0 || zone.SubAreas.Count is 0)
            return null;

        var tour = new HarvestTour(_sightings, _graphics);
        var maps = tour.Total(zone);
        var probable = tour.Probable(zone);
        var listed = zone.ListedIn(atlas).Count;
        var resources = settings.EverythingKnown
            ? new PreviewFigure(
                GameText.Of(HarvestTexts.Text("FigureNumber"), GameTextValue.Number(listed)),
                GameText.Counted(HarvestTexts.Text("FigureResourcesOne"), HarvestTexts.Text("FigureResourcesMany"), listed))
            : new PreviewFigure(
                GameText.Of(HarvestTexts.Text("FigureOutOf"), GameTextValue.Number(listed), GameTextValue.Number(settings.Resources.Distinct().Count())),
                GameText.Of(HarvestTexts.Text("FigureResourcesPresent")));
        var (bank, changes) = NearestBank(zone);
        var figures = new List<PreviewFigure>
        {
            new(GameText.Of(HarvestTexts.Text("FigureNumber"), GameTextValue.Number(maps)), GameText.Counted(HarvestTexts.Text("FigureMapsOne"), HarvestTexts.Text("FigureMapsMany"), maps)),
            new(GameText.Of(HarvestTexts.Text("FigureNumber"), GameTextValue.Number(probable)), GameText.Counted(HarvestTexts.Text("FigureProbableOne"), HarvestTexts.Text("FigureProbableMany"), probable)),
            resources,
            bank is null
                ? new(GameText.Of(HarvestTexts.Text("FigureNone")), GameText.Of(HarvestTexts.Text("FigureNoBank")))
                : new(GameText.Counted(HarvestTexts.Text("FigureBankOne"), HarvestTexts.Text("FigureBankMany"), changes), GameText.Of(HarvestTexts.Text("FigureBank"))),
        };

        return new SettingsPreview
        {
            Figures = [.. figures],
            Zone = tour.Maps(zone),
            LandmarkMapId = bank,
        };
    }

    private (long? Bank, int Changes) NearestBank(HarvestZone zone)
    {
        if (zone.Maps.Count is 0)
            return (null, 0);

        var pathfinder = _pathfinder.Value;
        var maps = zone.Maps.Keys.ToHashSet();
        var rules = new WorldPathRules { Facts = new CriterionFacts() };
        (long? Bank, int Changes) nearest = (null, int.MaxValue);

        foreach (var bank in _tables.Banks.Banks.Span)
        {
            if (!TryGetVertex(_world.Graph, bank.MapId, out var vertex) || !pathfinder.TryFind(vertex, maps, rules, out var path))
                continue;

            // The bank's door is one change more, from its front into its inside.
            var changes = path.Steps.Count + 1;

            if (changes < nearest.Changes)
                nearest = (bank.MapId, changes);
        }

        return nearest.Bank is null ? (null, 0) : nearest;
    }
}
