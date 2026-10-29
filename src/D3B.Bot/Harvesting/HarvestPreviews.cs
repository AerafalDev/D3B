using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.World;
using D3B.Engine.Criteria;
using D3B.Engine.World;

namespace D3B.Bot.Harvesting;

/// <summary>What harvesting settings lead to, for the app's profile editor: the zone's maps, its resources, its nearest bank (D127).</summary>
/// <remarks>
/// One per host. The path to the nearest bank is found from each bank's front to the zone, a door more into the bank, for
/// a character of whom nothing is known: criteria not met, no zaap. The app asks as the settings change: the last
/// preview is kept for the same zone and resources.
/// </remarks>
public sealed class HarvestPreviews
{
    private readonly GameWorld _world;
    private readonly GameTables _tables;
    private readonly Lazy<WorldPathfinder> _pathfinder;
    private readonly Lock _lock = new();
    private string? _key;
    private SettingsPreview? _last;

    /// <summary>Initializes a new instance of the <see cref="HarvestPreviews"/> class.</summary>
    /// <param name="world">The installed build's world.</param>
    /// <param name="tables">The installed build's tables: the professions' skills and the banks.</param>
    public HarvestPreviews(GameWorld world, GameTables tables)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tables);

        _world = world;
        _tables = tables;
        _pathfinder = new Lazy<WorldPathfinder>(() => new WorldPathfinder(world.Graph, world.Atlas), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static string KeyOf(HarvestSettings settings)
    {
        return $"{settings.EverythingKnown}|{string.Join(',', settings.Resources)}|{settings.Area}|{string.Join(',', settings.SubAreas)}";
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
        var zone = HarvestZone.Of(settings, atlas, _world.Graph, _tables.Jobs);

        if (zone.Targets.Count is 0 || zone.SubAreas.Count is 0)
            return null;

        var maps = zone.Maps.Count;
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
            resources,
            bank is null
                ? new(GameText.Of(HarvestTexts.Text("FigureNone")), GameText.Of(HarvestTexts.Text("FigureNoBank")))
                : new(GameText.Counted(HarvestTexts.Text("FigureBankOne"), HarvestTexts.Text("FigureBankMany"), changes), GameText.Of(HarvestTexts.Text("FigureBank"))),
        };

        return new SettingsPreview
        {
            Figures = [.. figures],
            Zone = [.. zone.Maps.Select(map => new ZoneMap(map.Key, ZoneMapState.ToVisit, map.Value))],
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
