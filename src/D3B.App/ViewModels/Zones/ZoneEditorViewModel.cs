using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls.WorldMaps;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Studio;
using D3B.Data.WorldMaps;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Zones;

/// <summary>A zone, edited on the world's map: maps painted in or erased, a sub-area added whole or taken out (D299, D394).</summary>
/// <remarks>
/// The map is the route editor's own control. A press on a square takes the first map there, the world's before the insides
/// (D299); the maps of a sub-area are all there at once, so that a sub-area is a click of its own. A map left out of a sub-area is
/// remembered as left out, so that painting it back is the reverse.
/// </remarks>
internal sealed partial class ZoneEditorViewModel : ObservableObject, IUndoable
{
    private readonly ZonesViewModel _page;
    private readonly RouteServices _services;
    private readonly UndoHistory<ZoneState> _history;
    private readonly Dictionary<int, int> _mapsOf = [];
    private ZoneAsset _saved;
    private ZoneState _state;
    private IReadOnlySet<long> _held = new HashSet<long>();
    private string? _run;
    private int _gestures;

    /// <summary>Gets the zone as it is now, changes not saved included, for the world's map to draw it by while another is edited.</summary>
    public ZoneAsset Current => _saved with { SubAreas = [.. _state.SubAreas], Maps = [.. _state.Maps], Excluded = [.. _state.Excluded] };

    /// <summary>Gets the zone's id.</summary>
    public string Id => _saved.Id;

    /// <summary>Gets what keys the zone's tab.</summary>
    public string TabKey => _saved.Id;

    /// <summary>Gets where the map reads its worlds and tiles from.</summary>
    public WorldMapSource Source { get; }

    /// <summary>Gets the worlds the layer can show.</summary>
    public IReadOnlyList<WorldChoiceViewModel> Worlds { get; private set; } = [];

    /// <summary>Gets the sub-areas the zone holds whole, to take out one by one.</summary>
    public ObservableCollection<ZoneSubAreaViewModel> SubAreas { get; } = [];

    /// <summary>Gets the zone's name, its file's.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; }

    /// <summary>Gets the world shown, one layer at a time (D299).</summary>
    [ObservableProperty]
    public partial int Layer { get; private set; }

    /// <summary>Gets the name of the world shown.</summary>
    [ObservableProperty]
    public partial string LayerName { get; private set; } = "";

    /// <summary>Gets what the map draws over its image.</summary>
    [ObservableProperty]
    public partial WorldMapScene Scene { get; private set; } = WorldMapScene.Empty;

    /// <summary>Gets or sets what a press on the map does.</summary>
    [ObservableProperty]
    public partial WorldMapTool Tool { get; set; } = WorldMapTool.Trace;

    /// <summary>Gets or sets whether the outline of each map shows.</summary>
    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    /// <summary>Gets whether the zone holds changes not saved.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Gets what lies under the pointer, in words.</summary>
    [ObservableProperty]
    public partial string Hud { get; private set; }

    /// <summary>Gets the zoom, as a percentage.</summary>
    [ObservableProperty]
    public partial string ZoomText { get; private set; } = "";

    /// <summary>Gets the zone's size, in words: its maps, its sub-areas, the maps left out.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Gets how many maps the zone holds.</summary>
    [ObservableProperty]
    public partial int MapCount { get; private set; }

    /// <summary>Gets whether the zone holds nothing.</summary>
    public bool IsEmpty => MapCount == 0 && _state.Excluded.IsEmpty;

    /// <summary>Gets whether the zone holds sub-areas, which a list names.</summary>
    public bool HasSubAreas => SubAreas.Count > 0;

    /// <summary>Gets whether there is a world layer to choose another from.</summary>
    public bool HasWorlds => Worlds.Count > 1;

    /// <summary>Gets whether the paint tool is chosen.</summary>
    public bool IsPainting => Tool == WorldMapTool.Trace;

    /// <summary>Gets whether the erase tool is chosen.</summary>
    public bool IsErasing => Tool == WorldMapTool.Erase;

    /// <summary>Gets whether the sub-area tool is chosen.</summary>
    public bool IsChoosingSubAreas => Tool == WorldMapTool.Region;

    /// <summary>Gets whether the select tool is chosen, which only moves the map.</summary>
    public bool IsSelecting => Tool == WorldMapTool.Select;

    /// <inheritdoc/>
    public bool CanUndo => _history.CanUndo;

    /// <inheritdoc/>
    public bool CanRedo => _history.CanRedo;

    /// <summary>Initializes a new instance of the <see cref="ZoneEditorViewModel"/> class.</summary>
    /// <param name="page">The zones, which hold what the editors read.</param>
    /// <param name="zone">The zone.</param>
    /// <exception cref="InvalidOperationException">The host does not run, so that the world is not known.</exception>
    public ZoneEditorViewModel(ZonesViewModel page, ZoneAsset zone)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(zone);

        _page = page;
        _saved = zone;
        _services = page.Services ?? throw new InvalidOperationException("The zones are edited once the host runs.");
        _state = StateOf(zone);
        _history = new UndoHistory<ZoneState>(_state, SameState);
        Source = new WorldMapSource(_services.Catalog, _services.Index, _services.ReadTile);
        Name = zone.Name;
        Hud = page.Localizer["ZoneHudNothing"];

        foreach (var position in _services.Atlas.Positions.Span)
            _mapsOf[position.SubAreaId] = _mapsOf.GetValueOrDefault(position.SubAreaId) + 1;

        Layer = FirstLayer();
        Relabel();
    }

    /// <summary>Raised when the map should show the whole zone.</summary>
    public event Action? FitRequested;

    /// <summary>Says the worlds, the sub-areas and the sizes again, in the language chosen.</summary>
    public void Relabel()
    {
        var worlds = new List<WorldChoiceViewModel>();

        foreach (var world in _services.Catalog.Worlds.Span)
        {
            var count = _services.Index.SquaresOf(world.Id).ToArray().Sum(square => square.Count);

            if (count > 0)
                worlds.Add(new WorldChoiceViewModel(world.Id, WorldName(world), count, _page.Localizer.Plural("RouteWorldMaps", count), world.Id == Layer, UseLayer));
        }

        Worlds = worlds;
        LayerName = worlds.FirstOrDefault(world => world.Id == Layer)?.Name ?? _page.Localizer.Format("RouteWorldNumber", Layer);
        OnPropertyChanged(nameof(Worlds));
        OnPropertyChanged(nameof(HasWorlds));
        Name = _saved.Name;
        Rebuild();
    }

    /// <summary>Takes the zone as the project holds it now, unless it holds changes not saved.</summary>
    /// <param name="zone">The zone.</param>
    public void Update(ZoneAsset zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var wasDirty = IsDirty;

        _saved = zone;
        Name = zone.Name;

        if (!wasDirty)
        {
            _state = StateOf(zone);

            // What was done belongs to the zone as it was: undoing it onto another would make a mixture.
            _history.Reset(_state);
            Moved();
        }

        Rebuild();
    }

    /// <summary>Says what is under the pointer on the map.</summary>
    /// <param name="hit">What it is.</param>
    public void Hover(WorldMapHit hit)
    {
        var localizer = _page.Localizer;

        if (hit.Kind is not (WorldMapHitKind.Square or WorldMapHitKind.Shared) || _services.Index.MapsAt(Layer, hit.X, hit.Y) is not { IsEmpty: false } maps)
        {
            Hud = localizer["ZoneHudNothing"];

            return;
        }

        var state = maps.ToArray().Any(_held.Contains) ? localizer["ZoneStateHeld"]
            : maps.ToArray().Any(_state.Excluded.Contains) ? localizer["ZoneStateLeftOut"]
            : localizer["ZoneStateOutside"];

        Hud = localizer.Format("ZoneHudSquare", localizer.Format("Coordinates", hit.X, hit.Y), SubAreaName(maps[0]), state);
    }

    /// <summary>Says the zoom.</summary>
    /// <param name="percent">The zoom, as a percentage of the client's full size.</param>
    public void Zoomed(double percent)
    {
        ZoomText = percent.ToString("N0", _page.Localizer.Culture) + " %";
    }

    /// <summary>Starts a gesture, a press and what it does until it is let go: the changes it makes are one to undo.</summary>
    public void BeginGesture()
    {
        _run = "gesture" + (++_gestures).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Ends the gesture.</summary>
    public void EndGesture()
    {
        _run = null;
    }

    /// <summary>Acts on the map at coordinates as the tool chosen does: the map's press, and each one a drag crosses.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="isDrag">Whether a drag crossed the coordinates; a sub-area is toggled by a press alone.</param>
    public void TraceSquare(int x, int y, bool isDrag)
    {
        var maps = _services.Index.MapsAt(Layer, x, y);

        if (maps.IsEmpty)
            return;

        switch (Tool)
        {
            case WorldMapTool.Trace:
                Paint(maps[0]);

                break;
            case WorldMapTool.Erase:
                foreach (var map in maps.ToArray())
                    Erase(map);

                break;
            case WorldMapTool.Region when !isDrag:
                ToggleSubArea(maps[0]);

                break;
        }
    }

    /// <summary>Adds a map to the zone by its id, such as one typed in or dropped.</summary>
    /// <param name="map">The map's id.</param>
    public void Add(long map)
    {
        if (_services.Atlas.TryGetPosition(map, out _))
            Paint(map);
    }

    /// <summary>Takes a sub-area out of the zone, every map of it.</summary>
    /// <param name="subArea">The sub-area's id.</param>
    public void RemoveSubArea(int subArea)
    {
        if (!_state.SubAreas.Contains(subArea))
            return;

        Change(_state with { SubAreas = _state.SubAreas.Remove(subArea), Excluded = _state.Excluded.Except(MapsOfSubArea(subArea)) });
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (_history.TryUndo(out var state))
            Restore(state);
    }

    /// <inheritdoc/>
    public void Redo()
    {
        if (_history.TryRedo(out var state))
            Restore(state);
    }

    /// <summary>Changes the layer to a world.</summary>
    /// <param name="world">The world's id.</param>
    public void UseLayer(int world)
    {
        if (world == Layer)
            return;

        Layer = world;
        Relabel();
        FitRequested?.Invoke();
    }

    [RelayCommand]
    private void SetTool(WorldMapTool tool)
    {
        Tool = tool;
    }

    [RelayCommand]
    private void Fit()
    {
        FitRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            _saved = await _services.Library.SaveAsync(_saved with { SubAreas = [.. _state.SubAreas], Maps = [.. _state.Maps], Excluded = [.. _state.Excluded] }, CancellationToken.None).ConfigureAwait(true);
            Name = _saved.Name;
            IsDirty = false;
            _page.Saved(this);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _page.Failed(exception);
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsDirty)
        {
            _page.EndEditing(this);

            return;
        }

        _page.AskToLeave(this, () => SaveCommand.Execute(null), () => _page.EndEditing(this));
    }

    private static ZoneState StateOf(ZoneAsset zone)
    {
        return new ZoneState([.. zone.SubAreas], [.. zone.Maps], [.. zone.Excluded]);
    }

    private static bool SameState(ZoneState first, ZoneState second)
    {
        return first.SubAreas.SetEquals(second.SubAreas) && first.Maps.SetEquals(second.Maps) && first.Excluded.SetEquals(second.Excluded);
    }

    private ZoneAsset Asset()
    {
        return _saved with { SubAreas = [.. _state.SubAreas], Maps = [.. _state.Maps], Excluded = [.. _state.Excluded] };
    }

    private void Paint(long map)
    {
        if (_held.Contains(map))
            return;

        // A map left out of a sub-area is taken back into it; any other is named.
        if (_state.Excluded.Contains(map))
            Change(_state with { Excluded = _state.Excluded.Remove(map) });
        else
            Change(_state with { Maps = _state.Maps.Add(map) });
    }

    private void Erase(long map)
    {
        if (!_held.Contains(map))
            return;

        var inSubArea = _services.Atlas.TryGetPosition(map, out var position) && _state.SubAreas.Contains(position.SubAreaId);

        Change(_state with { Maps = _state.Maps.Remove(map), Excluded = inSubArea ? _state.Excluded.Add(map) : _state.Excluded });
    }

    private void ToggleSubArea(long map)
    {
        if (!_services.Atlas.TryGetPosition(map, out var position))
            return;

        var subArea = position.SubAreaId;
        var its = MapsOfSubArea(subArea);

        // Whole or not at all: the maps named of it are redundant, and those left out of it no longer matter once it goes.
        Change(_state.SubAreas.Contains(subArea)
            ? _state with { SubAreas = _state.SubAreas.Remove(subArea), Excluded = _state.Excluded.Except(its) }
            : _state with { SubAreas = _state.SubAreas.Add(subArea), Maps = _state.Maps.Except(its), Excluded = _state.Excluded.Except(its) });
    }

    private HashSet<long> MapsOfSubArea(int subArea)
    {
        var maps = new HashSet<long>();

        foreach (var position in _services.Atlas.Positions.Span)
        {
            if (position.SubAreaId == subArea)
                maps.Add(position.MapId);
        }

        return maps;
    }

    private void Change(ZoneState state)
    {
        if (_history.Record(state, _run))
        {
            _state = state;
            Moved();
        }

        Rebuild();
    }

    private void Restore(ZoneState state)
    {
        _state = state;
        Moved();
        Rebuild();
    }

    private void Moved()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void Rebuild()
    {
        var localizer = _page.Localizer;

        _held = ZoneMaps.Resolve(Asset(), _services.Atlas);
        MapCount = _held.Count;
        IsDirty = !SameState(_state, StateOf(_saved));
        Summary = string.Join(" · ", localizer.Plural("ZoneSummaryMaps", _held.Count), localizer.Plural("ZoneSummarySubAreas", _state.SubAreas.Count), localizer.Plural("ZoneSummaryLeftOut", _state.Excluded.Count));

        SubAreas.Clear();

        foreach (var id in _state.SubAreas)
            SubAreas.Add(new ZoneSubAreaViewModel(id, SubAreaNameOf(id), localizer.Plural("RouteWorldMaps", _mapsOf.GetValueOrDefault(id)), RemoveSubArea));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasSubAreas));
        BuildScene();
    }

    // The squares of the layer shown: those holding a map of the zone, else those holding a map it leaves out.
    private void BuildScene()
    {
        var held = new HashSet<(int X, int Y)>();
        var leftOut = new HashSet<(int X, int Y)>();

        foreach (var map in _held)
        {
            if (_services.Index.TryPlace(map, out var layer, out var x, out var y) && layer == Layer)
                held.Add((x, y));
        }

        foreach (var map in _state.Excluded)
        {
            if (_services.Index.TryPlace(map, out var layer, out var x, out var y) && layer == Layer && !held.Contains((x, y)))
                leftOut.Add((x, y));
        }

        Scene = new WorldMapScene([], [], [.. held.Select(square => new WorldMapZoneSquare(square.X, square.Y, false)), .. leftOut.Select(square => new WorldMapZoneSquare(square.X, square.Y, true))]);
    }

    private int FirstLayer()
    {
        foreach (var map in _held.Count > 0 ? _held : _state.Maps.Concat(_state.Excluded).ToHashSet())
        {
            if (_services.Index.TryPlace(map, out var layer, out _, out _))
                return layer;
        }

        foreach (var subArea in _state.SubAreas)
        {
            foreach (var map in MapsOfSubArea(subArea))
            {
                if (_services.Index.TryPlace(map, out var layer, out _, out _))
                    return layer;
            }
        }

        return _services.Catalog.TryGet(1, out _) ? 1 : _services.Catalog.Worlds.Span[0].Id;
    }

    private string WorldName(WorldMap world)
    {
        return _services.Texts is { } texts && texts.TryGet(_page.Localizer.GameLanguage, world.NameId, out var name)
            ? name
            : _page.Localizer.Format("RouteWorldNumber", world.Id);
    }

    private string SubAreaName(long map)
    {
        return _services.Atlas.TryGetPosition(map, out var position) ? SubAreaNameOf(position.SubAreaId) : _page.Localizer.Format("MapNumber", map);
    }

    private string SubAreaNameOf(int id)
    {
        var localizer = _page.Localizer;

        return _services.Texts is { } texts && _services.Atlas.TryGetSubArea(id, out var subArea) && texts.TryGet(localizer.GameLanguage, subArea.NameId, out var name)
            ? name
            : localizer.Format("SubArea", id);
    }

    partial void OnToolChanged(WorldMapTool value)
    {
        OnPropertyChanged(nameof(IsPainting));
        OnPropertyChanged(nameof(IsErasing));
        OnPropertyChanged(nameof(IsChoosingSubAreas));
        OnPropertyChanged(nameof(IsSelecting));
    }

    private sealed record ZoneState(ImmutableSortedSet<int> SubAreas, ImmutableSortedSet<long> Maps, ImmutableSortedSet<long> Excluded);
}
