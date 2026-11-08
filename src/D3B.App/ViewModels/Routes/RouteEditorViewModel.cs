using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls.WorldMaps;
using D3B.App.ViewModels.Maps;
using D3B.App.ViewModels.Settings;
using D3B.App.ViewModels.Studio;
using D3B.Bot.Npcs;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Data.WorldMaps;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Routes;

/// <summary>A route edited on the world's map: its anchors, the world's path between them, and how it goes through them (D299, D371).</summary>
/// <remarks>
/// <para>
/// The anchors are the truth; the maps between two are found by the world's path, drawn apart and found again when an anchor
/// moves, and a path point pinned becomes an anchor. They are added by pressing or painting the map, by typing a map's id or
/// coordinates, or by choosing among the maps that share coordinates. A gap no path crosses is red, and said in the
/// inspector. Recording, the actions of each map and running a route are 7d3.
/// </para>
/// <para>
/// The path is searched off the interface's thread, one search per leg that moved, and shown once found: the scene meanwhile
/// stays as it was. The map's view hears what is pressed through this model, and is given a <see cref="WorldMapScene"/>.
/// </para>
/// </remarks>
internal sealed partial class RouteEditorViewModel : ObservableObject, IUndoable
{
    private const int UndoDepth = 200;

    private static readonly Regex _coordinates = CoordinatesPattern();
    private static readonly Regex _mapId = MapIdPattern();

    private readonly RoutesViewModel _page;
    private readonly List<RouteAnchor> _anchors;
    private readonly List<int> _stationAnchors = [];
    private readonly List<int> _sceneLegs = [];
    private readonly List<Snapshot> _undo = [];
    private readonly List<Snapshot> _redo = [];
    private RouteAsset _saved;
    private IReadOnlyList<RouteLeg> _legs = [];
    private long _paintedFrom;
    private int _version;
    private Task _planning = Task.CompletedTask;
    private Snapshot _current;
    private bool _inGesture;
    private bool _gestureRecorded;
    private bool _keepActionForms;
    private RouteActionPickerViewModel? _picker;

    /// <summary>Gets the route's id.</summary>
    public string Id => _saved.Id;

    /// <summary>Gets the name of the route's tab.</summary>
    public string TabKey => _saved.Id;

    /// <summary>Gets the route's anchors as steps, in order.</summary>
    public ObservableCollection<RouteStepViewModel> Steps { get; } = [];

    /// <summary>Gets the actions of the step chosen, in order (D377).</summary>
    public ObservableCollection<RouteActionViewModel> Actions { get; } = [];

    /// <summary>Gets the actions the menu offers to add to a step.</summary>
    public IReadOnlyList<RouteActionChoiceViewModel> OfferedActions { get; private set; } = [];

    /// <summary>Gets the worlds the layer can show.</summary>
    public IReadOnlyList<WorldChoiceViewModel> Worlds { get; private set; } = [];

    /// <summary>Gets where the map reads its worlds and tiles from.</summary>
    public WorldMapSource Source { get; }

    /// <summary>Gets the route's name, its file's.</summary>
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
    public partial WorldMapTool Tool { get; set; }

    /// <summary>Gets or sets whether the outline of each map shows.</summary>
    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    /// <summary>Gets or sets whether the mark of coordinates that several maps share shows.</summary>
    [ObservableProperty]
    public partial bool ShowShared { get; set; } = true;

    /// <summary>Gets how the route goes through its anchors.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoop), nameof(IsOneWay), nameof(IsRoundTrip))]
    public partial RouteMode Mode { get; private set; }

    /// <summary>Gets what the route does with a map no path reaches.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SkipsUnreachable), nameof(FailsOnUnreachable))]
    public partial RouteUnreachable Unreachable { get; private set; }

    /// <summary>Gets the step chosen, or -1.</summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; private set; } = -1;

    /// <summary>Gets whether the route holds changes not saved.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Gets or sets what is typed to add an anchor: a map's id, or coordinates.</summary>
    [ObservableProperty]
    public partial string AddText { get; set; } = "";

    /// <summary>Gets whether what was typed names no map, which the box says in red.</summary>
    [ObservableProperty]
    public partial bool AddFailed { get; private set; }

    /// <summary>Gets the choice among maps that share coordinates, while one is open.</summary>
    [ObservableProperty]
    public partial SharedChoiceViewModel? Choice { get; private set; }

    /// <summary>Gets what lies under the pointer, in words.</summary>
    [ObservableProperty]
    public partial string Hud { get; private set; }

    /// <summary>Gets the zoom, as a percentage.</summary>
    [ObservableProperty]
    public partial string ZoomText { get; private set; } = "";

    /// <summary>Gets the first gap no path crosses, said with how to read it, or <see langword="null"/> when there is none.</summary>
    [ObservableProperty]
    public partial string? Problem { get; private set; }

    /// <summary>Gets the step the first gap starts from, or -1.</summary>
    [ObservableProperty]
    public partial int ProblemStep { get; private set; } = -1;

    /// <summary>Gets how many steps the route has.</summary>
    [ObservableProperty]
    public partial int StepCount { get; private set; }

    /// <summary>Gets how many maps the route goes through, the path's included.</summary>
    [ObservableProperty]
    public partial int MapCount { get; private set; }

    /// <summary>Gets how many actions the route plays in all.</summary>
    [ObservableProperty]
    public partial int ActionTotal { get; private set; }

    /// <summary>Gets how many gaps no path crosses.</summary>
    [ObservableProperty]
    public partial int GapCount { get; private set; }

    /// <summary>Gets whether the route goes round: from the last anchor back to the first, and again.</summary>
    public bool IsLoop => Mode == RouteMode.Loop;

    /// <summary>Gets whether the route goes once from the first anchor to the last.</summary>
    public bool IsOneWay => Mode == RouteMode.OneWay;

    /// <summary>Gets whether the route goes to the last anchor and back.</summary>
    public bool IsRoundTrip => Mode == RouteMode.RoundTrip;

    /// <summary>Gets whether the route passes a map no path reaches.</summary>
    public bool SkipsUnreachable => Unreachable == RouteUnreachable.Skip;

    /// <summary>Gets whether a map no path reaches fails the route.</summary>
    public bool FailsOnUnreachable => Unreachable == RouteUnreachable.Fail;

    /// <summary>Gets whether the route has no anchor.</summary>
    public bool IsEmpty => _anchors.Count == 0;

    /// <summary>Gets whether a step is chosen, whose actions the inspector shows.</summary>
    public bool HasSelectedStep => SelectedIndex >= 0 && SelectedIndex < _anchors.Count;

    /// <summary>Gets the title of the chosen step's actions, such as "Actions on step 3".</summary>
    public string ActionsTitle => HasSelectedStep ? _page.Localizer.Format("RouteActionsTitle", SelectedIndex + 1) : "";

    /// <summary>Gets whether the chosen step has no action.</summary>
    public bool HasNoActions => HasSelectedStep && Actions.Count == 0;

    /// <summary>Gets whether actions can be added: the host holds the kinds of nodes.</summary>
    public bool CanAddActions => HasSelectedStep && OfferedActions.Count > 0;

    /// <summary>Gets whether a gap no path crosses is said.</summary>
    public bool HasProblem => Problem is not null;

    /// <summary>Gets whether the choice among shared maps is open.</summary>
    public bool HasChoice => Choice is not null;

    /// <summary>Gets whether the route has a world layer shown to choose another from.</summary>
    public bool HasWorlds => Worlds.Count > 1;

    /// <summary>Gets whether the trace tool is chosen.</summary>
    public bool IsTracing => Tool == WorldMapTool.Trace;

    /// <summary>Gets whether the select tool is chosen.</summary>
    public bool IsSelecting => Tool == WorldMapTool.Select;

    /// <inheritdoc/>
    public bool CanUndo => _undo.Count > 0;

    /// <inheritdoc/>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Gets what the path search has not finished.</summary>
    public Task Planned => _planning;

    /// <summary>Initializes a new instance of the <see cref="RouteEditorViewModel"/> class.</summary>
    /// <param name="page">The routes, which hold what the editors read.</param>
    /// <param name="route">The route.</param>
    public RouteEditorViewModel(RoutesViewModel page, RouteAsset route)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(route);

        _page = page;
        _saved = route;
        _anchors = [.. route.Anchors];

        var services = page.Services ?? throw new InvalidOperationException("The routes are edited once the host runs.");

        Source = new WorldMapSource(services.Catalog, services.Index, services.ReadTile);
        Name = route.Name;
        Mode = route.Mode;
        Unreachable = route.Unreachable;
        _current = Take();
        Hud = page.Localizer["RouteHudNothing"];
        Layer = FirstLayer();
        Relabel();
        Rebuild();
    }

    /// <summary>Raised when the map should show the coordinates of a step, such as when it is chosen from the list.</summary>
    public event Action<int, int>? RevealRequested;

    /// <summary>Raised when the map should show the whole route.</summary>
    public event Action? FitRequested;

    /// <summary>Says the worlds, the steps and the zones again, in the language chosen.</summary>
    public void Relabel()
    {
        var services = _page.Services!;
        var worlds = new List<WorldChoiceViewModel>();

        foreach (var world in services.Catalog.Worlds.Span)
        {
            var count = services.Index.SquaresOf(world.Id).ToArray().Sum(square => square.Count);

            if (count == 0)
                continue;

            worlds.Add(new WorldChoiceViewModel(world.Id, WorldName(world), count, _page.Localizer.Plural("RouteWorldMaps", count), world.Id == Layer, UseLayer));
        }

        Worlds = worlds;
        LayerName = worlds.FirstOrDefault(world => world.Id == Layer)?.Name ?? _page.Localizer.Format("RouteWorldNumber", Layer);
        OnPropertyChanged(nameof(Worlds));
        OnPropertyChanged(nameof(HasWorlds));
        Name = _saved.Name;
        OfferActions();
        Rebuild();
    }

    /// <summary>Takes the route as the project holds it now, unless it holds changes not saved.</summary>
    /// <param name="route">The route.</param>
    public void Update(RouteAsset route)
    {
        ArgumentNullException.ThrowIfNull(route);

        var wasDirty = IsDirty;

        _saved = route;
        Name = route.Name;

        if (!wasDirty)
        {
            _anchors.Clear();
            _anchors.AddRange(route.Anchors);
            Mode = route.Mode;
            Unreachable = route.Unreachable;

            // What was done belongs to the route as it was: undoing it onto another would make a mixture.
            _undo.Clear();
            _redo.Clear();
            _current = Take();
            Moved();
        }

        Rebuild();
    }

    /// <summary>Says what is under the pointer on the map.</summary>
    /// <param name="hit">What it is.</param>
    public void Hover(WorldMapHit hit)
    {
        var localizer = _page.Localizer;

        Hud = hit.Kind switch
        {
            WorldMapHitKind.Square or WorldMapHitKind.Shared => Describe(hit.X, hit.Y),
            WorldMapHitKind.Station when hit.Station >= 0 && hit.Station < _stationAnchors.Count => HudOfStep(_stationAnchors[hit.Station]),
            WorldMapHitKind.PathPoint => localizer["RouteHudPath"],
            _ => localizer["RouteHudNothing"],
        };
    }

    /// <summary>Says the zoom.</summary>
    /// <param name="percent">The zoom, as a percentage of the client's full size.</param>
    public void Zoomed(double percent)
    {
        ZoomText = percent.ToString("N0", _page.Localizer.Culture) + " %";
    }

    /// <summary>Chooses a step from the map: its pill was pressed.</summary>
    /// <param name="station">The station's place in the scene.</param>
    public void SelectStation(int station)
    {
        if (station >= 0 && station < _stationAnchors.Count)
            Select(_stationAnchors[station]);
    }

    /// <summary>Moves the step a station is of onto other coordinates, while it is dragged.</summary>
    /// <param name="station">The station's place in the scene.</param>
    /// <param name="x">The coordinates dragged onto.</param>
    /// <param name="y">The coordinates dragged onto.</param>
    public void MoveStation(int station, int x, int y)
    {
        if (station < 0 || station >= _stationAnchors.Count)
            return;

        var maps = _page.Services!.Index.MapsAt(Layer, x, y);

        if (maps.IsEmpty)
            return;

        var anchor = _stationAnchors[station];

        _anchors[anchor] = _anchors[anchor] with { MapId = _page.Services.Planner.Primary(maps), IsPinned = false };
        Changed();
    }

    /// <summary>Adds an anchor on the map a trace pressed, or a drag crossed.</summary>
    /// <param name="x">The coordinates.</param>
    /// <param name="y">The coordinates.</param>
    /// <param name="isDrag">Whether a drag crossed the coordinates: it adds an anchor only when the map is a neighbour of the map crossed before by a real exit, else the path fills the gap (D299).</param>
    public void TraceSquare(int x, int y, bool isDrag)
    {
        var services = _page.Services!;
        var maps = services.Index.MapsAt(Layer, x, y);

        if (maps.IsEmpty)
            return;

        var map = services.Planner.Primary(maps);
        var from = _paintedFrom;

        // A drag goes on from the map it crossed last, whether or not that one became an anchor: a square no exit joins is left to
        // the path, and the drag does not stop there.
        _paintedFrom = map;

        if (_anchors.Exists(anchor => anchor.MapId == map))
            return;

        if (isDrag && from != 0)
        {
            var leg = services.Planner.Find(from, map);

            if (leg.Kind != RouteLegKind.Path || !leg.Between.IsEmpty)
                return;
        }

        AddAnchor(new RouteAnchor { MapId = map });
    }

    /// <summary>Pins a map of the path, which becomes an anchor between the two it lay between.</summary>
    /// <param name="sceneLeg">The leg's place in the scene.</param>
    /// <param name="x">The map's coordinates.</param>
    /// <param name="y">The map's coordinates.</param>
    public void PinPathPoint(int sceneLeg, int x, int y)
    {
        if (sceneLeg < 0 || sceneLeg >= _sceneLegs.Count)
            return;

        var leg = _sceneLegs[sceneLeg];
        var index = _page.Services!.Index;
        var map = _legs[leg].Between.FirstOrDefault(between => index.TryPlace(between, out var layer, out var bx, out var by) && layer == Layer && bx == x && by == y);

        if (map == 0)
            return;

        AddAnchor(new RouteAnchor { MapId = map, IsPinned = true }, leg + 1);
    }

    /// <summary>Opens the choice among the maps that share coordinates.</summary>
    /// <param name="x">The coordinates.</param>
    /// <param name="y">The coordinates.</param>
    /// <param name="at">The point of the view the choice opens at, or <see langword="null"/>.</param>
    public void OpenChoice(int x, int y, Point? at)
    {
        var maps = _page.Services!.Index.MapsAt(Layer, x, y).ToArray();

        if (maps.Length == 0)
            return;

        var localizer = _page.Localizer;
        var items = maps.Select(map => new MapChoiceViewModel(map, Zone(map), IsInside(map), IsInside(map) ? localizer["RouteSharedInside"] : localizer["RouteSharedOutside"])).ToList();

        Choice = new SharedChoiceViewModel(localizer.Format("RouteSharedTitle", maps.Length, localizer.Format("Coordinates", x, y)), items, at, Choose, CloseChoice);
    }

    /// <summary>Selects a step, the map showing it.</summary>
    /// <param name="index">The step's place in the route.</param>
    public void Select(int index)
    {
        SelectedIndex = index >= 0 && index < _anchors.Count ? index : -1;
        MarkSelected();
        BuildScene();
    }

    /// <summary>Selects a step from the list, the map moving to show it.</summary>
    /// <param name="index">The step's place in the route.</param>
    public void Reveal(int index)
    {
        Select(index);

        if (index >= 0 && index < _anchors.Count && _page.Services!.Index.TryPlace(_anchors[index].MapId, out var layer, out var x, out var y))
        {
            if (layer != Layer)
                UseLayer(layer);

            RevealRequested?.Invoke(x, y);
        }
    }

    /// <summary>Removes a step.</summary>
    /// <param name="index">The step's place in the route.</param>
    public void Remove(int index)
    {
        if (index < 0 || index >= _anchors.Count)
            return;

        _anchors.RemoveAt(index);
        SelectedIndex = Math.Min(SelectedIndex, _anchors.Count - 1);
        Changed();
    }

    /// <summary>Removes the step chosen: Delete.</summary>
    public void RemoveSelected()
    {
        Remove(SelectedIndex);
    }

    /// <summary>Moves the step chosen in the route: Alt with the up and down arrows.</summary>
    /// <param name="delta">How many places towards the end, negative towards the start.</param>
    public void MoveSelected(int delta)
    {
        var from = SelectedIndex;
        var to = from + delta;

        if (from < 0 || from >= _anchors.Count || to < 0 || to >= _anchors.Count || to == from)
            return;

        var anchor = _anchors[from];

        _anchors.RemoveAt(from);
        _anchors.Insert(to, anchor);
        SelectedIndex = to;
        Changed();
    }

    /// <summary>Chooses another step and shows it on the map: the page keys.</summary>
    /// <param name="delta">How many places towards the end, negative towards the start.</param>
    public void StepBy(int delta)
    {
        if (_anchors.Count == 0)
            return;

        Reveal(Math.Clamp(SelectedIndex < 0 ? (delta > 0 ? 0 : _anchors.Count - 1) : SelectedIndex + delta, 0, _anchors.Count - 1));
    }

    /// <summary>Starts a gesture, a press and what it does until it is let go: the changes it makes are one to undo.</summary>
    public void BeginGesture()
    {
        _inGesture = true;
        _gestureRecorded = false;
    }

    /// <summary>Ends the gesture.</summary>
    public void EndGesture()
    {
        _inGesture = false;
        _gestureRecorded = false;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        _redo.Add(_current);
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    /// <inheritdoc/>
    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        _undo.Add(_current);
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    /// <summary>Changes the layer to a world.</summary>
    /// <param name="world">The world's id.</param>
    public void UseLayer(int world)
    {
        if (world == Layer)
            return;

        Layer = world;
        Choice = null;
        Relabel();
        FitRequested?.Invoke();
    }

    [RelayCommand]
    private void ChooseAction()
    {
        if (HasSelectedStep)
            OpenActionPicker(SelectedIndex);
    }

    [RelayCommand]
    private void AddAction(string? node)
    {
        var catalog = _page.Services?.Nodes;

        if (node is null || catalog is null || !catalog.TryGet(node, out var definition) || !HasSelectedStep)
            return;

        var anchor = _anchors[SelectedIndex];

        _anchors[SelectedIndex] = anchor with { Actions = anchor.Actions.Add(RouteActionCalls.Create(definition)) };
        Changed();
    }

    [RelayCommand]
    private void SetMode(RouteMode mode)
    {
        Mode = mode;
        Changed();
    }

    [RelayCommand]
    private void SetUnreachable(RouteUnreachable policy)
    {
        Unreachable = policy;
        Changed();
    }

    [RelayCommand]
    private void SetTool(WorldMapTool tool)
    {
        Tool = tool;
    }

    [RelayCommand]
    private void ShowProblem()
    {
        if (ProblemStep >= 0)
            Reveal(ProblemStep);
    }

    [RelayCommand]
    private void Add()
    {
        var text = AddText.Trim();
        var services = _page.Services!;

        if (text.Length == 0)
            return;

        var pair = _coordinates.Match(text);

        if (pair.Success)
        {
            var x = int.Parse(pair.Groups[1].Value, CultureInfo.InvariantCulture);
            var y = int.Parse(pair.Groups[2].Value, CultureInfo.InvariantCulture);
            var maps = services.Index.MapsAt(Layer, x, y);

            if (maps.IsEmpty)
            {
                AddFailed = true;

                return;
            }

            AddText = "";

            if (maps.Length == 1)
                AddAnchor(new RouteAnchor { MapId = maps[0] });
            else
                OpenChoice(x, y, null);

            return;
        }

        if (_mapId.IsMatch(text) && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var map) && services.Index.TryPlace(map, out var layer, out _, out _))
        {
            AddText = "";

            if (layer != Layer)
                UseLayer(layer);

            AddAnchor(new RouteAnchor { MapId = map });

            return;
        }

        AddFailed = true;
    }

    [RelayCommand]
    private void CloseChoice()
    {
        Choice = null;
    }

    [RelayCommand]
    private void Fit()
    {
        FitRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_page.Services is not { } services)
            return;

        try
        {
            _saved = await services.Library.SaveAsync(_saved with { Anchors = [.. _anchors], Mode = Mode, Unreachable = Unreachable }, CancellationToken.None);
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

    [GeneratedRegex(@"^\[?\s*(-?\d{1,4})\s*[,;\s]\s*(-?\d{1,4})\s*\]?$", RegexOptions.CultureInvariant)]
    private static partial Regex CoordinatesPattern();

    [GeneratedRegex(@"^\d{5,}$", RegexOptions.CultureInvariant)]
    private static partial Regex MapIdPattern();

    private static bool Contains(List<(int X, int Y)> squares, int x, int y)
    {
        return squares.Count > 0 && squares[^1] == (x, y);
    }

    private string WorldName(WorldMap world)
    {
        var services = _page.Services!;

        return services.Texts is { } texts && texts.TryGet(_page.Localizer.GameLanguage, world.NameId, out var name)
            ? name
            : _page.Localizer.Format("RouteWorldNumber", world.Id);
    }

    private int FirstLayer()
    {
        var services = _page.Services!;

        foreach (var anchor in _anchors)
        {
            if (services.Index.TryPlace(anchor.MapId, out var layer, out _, out _))
                return layer;
        }

        return services.Catalog.TryGet(1, out _) ? 1 : services.Catalog.Worlds.Span[0].Id;
    }

    private bool IsInside(long map)
    {
        return _page.Services!.Atlas.TryGetPosition(map, out var position) && position.WorldMap < 0;
    }

    private string Place(long map)
    {
        var localizer = _page.Localizer;

        if (!_page.Services!.Atlas.TryGetPosition(map, out var position))
            return localizer.Format("MapNumber", map);

        var coordinates = localizer.Format("Coordinates", position.X, position.Y);

        return position.WorldMap < 0 ? localizer.Format("MapInside", coordinates) : coordinates;
    }

    private string Zone(long map)
    {
        var services = _page.Services!;
        var localizer = _page.Localizer;

        if (!services.Atlas.TryGetPosition(map, out var position))
            return localizer.Format("MapNumber", map);

        return services.Texts is { } texts && services.Atlas.TryGetSubArea(position.SubAreaId, out var subArea) && texts.TryGet(localizer.GameLanguage, subArea.NameId, out var name)
            ? name
            : localizer.Format("SubArea", position.SubAreaId);
    }

    private string Describe(int x, int y)
    {
        var maps = _page.Services!.Index.MapsAt(Layer, x, y);
        var localizer = _page.Localizer;
        var coordinates = localizer.Format("Coordinates", x, y);

        if (maps.IsEmpty)
            return coordinates;

        var zone = Zone(maps[0]);

        return maps.Length == 1
            ? localizer.Format("RouteHudMap", coordinates, maps[0].ToString("N0", localizer.Culture), zone)
            : localizer.Format("RouteHudMaps", coordinates, maps.Length, zone);
    }

    private void AddActionNode(string node)
    {
        if (_picker is not null)
        {
            _page.Dialogs.Close(_picker);
            _picker = null;
        }

        AddAction(node);
    }

    private void OpenActionPicker(int index)
    {
        Select(index);

        if (OfferedActions.Count == 0)
            return;

        _picker = new RouteActionPickerViewModel(_page.Dialogs, _page.Localizer["RouteActionAdd"], _page.Localizer["Cancel"], OfferedActions);
        _page.Dialogs.Show(_picker);
    }

    private void RemoveAction(int index)
    {
        if (!HasSelectedStep || index < 0 || index >= _anchors[SelectedIndex].Actions.Length)
            return;

        _anchors[SelectedIndex] = _anchors[SelectedIndex] with { Actions = _anchors[SelectedIndex].Actions.RemoveAt(index) };
        Changed();
    }

    private void MoveAction(int index, int delta)
    {
        var to = index + delta;

        if (!HasSelectedStep || index < 0 || to < 0 || index >= _anchors[SelectedIndex].Actions.Length || to >= _anchors[SelectedIndex].Actions.Length)
            return;

        var actions = _anchors[SelectedIndex].Actions;
        var moved = actions[index];

        _anchors[SelectedIndex] = _anchors[SelectedIndex] with { Actions = actions.RemoveAt(index).Insert(to, moved) };
        Changed();
    }

    private void ActionEdited(int index, NodeDefinition definition, SettingsEditor editor)
    {
        if (!HasSelectedStep || index >= _anchors[SelectedIndex].Actions.Length || index >= Actions.Count)
            return;

        var anchor = _anchors[SelectedIndex];
        var made = RouteActionCalls.Make(definition, editor.Draft);

        if (!RouteAnchor.SameCall(anchor.Actions[index], made))
        {
            _anchors[SelectedIndex] = anchor with { Actions = anchor.Actions.SetItem(index, made) };

            // The forms stay as they are: rebuilding them under the hand that types in one would take its focus.
            _keepActionForms = true;

            try
            {
                Changed();
            }
            finally
            {
                _keepActionForms = false;
            }
        }

        Actions[index].Say();
    }

    private static string NameKeyOf(string node)
    {
        return node switch
        {
            RouteActions.Harvest => "RouteNameHarvest",
            RouteActions.Fight => "RouteNameFight",
            RouteActions.Door => "RouteNameDoor",
            RouteActions.Exit => "RouteNameExit",
            RouteActions.Wait => "RouteNameWait",
            RouteActions.Zaap => "RouteNameZaap",
            RouteActions.Npc => "RouteNameNpc",
            _ => "RouteNameBag",
        };
    }

    private void OfferActions()
    {
        var catalog = _page.Services?.Nodes;
        var culture = _page.Localizer.Culture;
        var offered = new List<RouteActionChoiceViewModel>();

        if (catalog is not null)
        {
            foreach (var node in RouteActions.Offered)
            {
                if (catalog.TryGet(node, out var definition))
                    offered.Add(new RouteActionChoiceViewModel(node, _page.Localizer[NameKeyOf(node)], definition.Help?.In(culture) ?? "", AddActionNode));
            }
        }

        OfferedActions = offered;
        OnPropertyChanged(nameof(OfferedActions));
        OnPropertyChanged(nameof(CanAddActions));
    }

    private string HudOfStep(int index)
    {
        var localizer = _page.Localizer;
        var place = Place(_anchors[index].MapId);
        var count = _anchors[index].Actions.Length;

        return count == 0 ? localizer.Format("RouteHudStep", index + 1, place) : localizer.Format("RouteHudStepActions", index + 1, place, localizer.Plural("RouteActionsCount", count));
    }

    private void BuildActions()
    {
        Actions.Clear();

        var services = _page.Services!;

        if (HasSelectedStep)
        {
            var anchor = _anchors[SelectedIndex];
            var localizer = _page.Localizer;
            var names = new GameNames(services.Texts, localizer);
            MapData? map = null;

            services.Maps?.TryGet(anchor.MapId, out map);

            var npcs = services.Provider?.GetService<KnownNpcs>();
            var places = new MapNames(localizer, services.Provider?.GetService<GameWorld>(), services.Texts, services.Provider?.GetService<GameTables>());

            for (var i = 0; i < anchor.Actions.Length; i++)
            {
                var call = anchor.Actions[i];
                NodeDefinition? definition = null;
                SettingsEditor? editor = null;

                if (services.Nodes is { } catalog && catalog.TryGet(call.Node, out definition))
                {
                    var made = definition;
                    var index = i;
                    var created = new SettingsEditor(made.Values, RouteActionCalls.Read(made, call).Settings, services.Game, names, localizer, services.Provider, [], _page.Dialogs) { Map = map, Npcs = npcs, NpcNames = new NpcNames(localizer, services.Provider?.GetService<GameTables>(), services.Texts), Place = places.Place };

                    created.Edited += (_, _) => ActionEdited(index, made, created);
                    editor = created;
                }

                Actions.Add(new RouteActionViewModel(i, anchor.Actions.Length, call.Node, definition, editor, localizer, (kind, id) => NameOf(names, kind, id), RemoveAction, MoveAction));
            }
        }

        NotifyActions();
    }

    private string NameOf(GameNames names, SettingKind kind, int id)
    {
        var game = _page.Services!.Game;

        return game is not null && game.Options(kind).FirstOrDefault(option => option.Id == id) is { } reference
            ? names.Name(kind, reference)
            : "#" + id.ToString(CultureInfo.InvariantCulture);
    }

    private void NotifyActions()
    {
        OnPropertyChanged(nameof(HasSelectedStep));
        OnPropertyChanged(nameof(ActionsTitle));
        OnPropertyChanged(nameof(HasNoActions));
        OnPropertyChanged(nameof(CanAddActions));
    }

    private void AddAnchor(RouteAnchor anchor, int? at = null)
    {
        var index = Math.Clamp(at ?? _anchors.Count, 0, _anchors.Count);

        _anchors.Insert(index, anchor);
        SelectedIndex = index;
        Changed();
    }

    private void Choose(MapChoiceViewModel map)
    {
        Choice = null;
        AddAnchor(new RouteAnchor { MapId = map.MapId });
    }

    private void Changed()
    {
        var now = Take();

        if (!now.Equals(_current))
        {
            // A gesture, a drag across many maps, is one change to take back, recorded at its first.
            if (!_inGesture || !_gestureRecorded)
            {
                _undo.Add(_current);

                if (_undo.Count > UndoDepth)
                    _undo.RemoveAt(0);

                _gestureRecorded = _inGesture;
            }

            _redo.Clear();
            _current = now;
            Moved();
        }

        Rebuild();
        RecomputeDirty();
    }

    private Snapshot Take()
    {
        return new Snapshot([.. _anchors], Mode, Unreachable);
    }

    private void Restore(Snapshot state)
    {
        _anchors.Clear();
        _anchors.AddRange(state.Anchors);
        Mode = state.Mode;
        Unreachable = state.Unreachable;
        _current = state;
        Choice = null;
        SelectedIndex = Math.Min(SelectedIndex, _anchors.Count - 1);
        Moved();
        Rebuild();
        RecomputeDirty();
    }

    private void Moved()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void RecomputeDirty()
    {
        IsDirty = Mode != _saved.Mode || Unreachable != _saved.Unreachable || !_anchors.SequenceEqual(_saved.Anchors);
    }

    private void Rebuild()
    {
        StepCount = _anchors.Count;
        ActionTotal = _anchors.Sum(anchor => anchor.Actions.Length);
        OnPropertyChanged(nameof(IsEmpty));
        BuildSteps();
        BuildScene();

        if (!_keepActionForms)
            BuildActions();

        var version = ++_version;
        var maps = _anchors.ConvertAll(anchor => anchor.MapId);
        var mode = Mode;

        _planning = PlanAsync(version, maps, mode);
    }

    private async Task PlanAsync(int version, List<long> maps, RouteMode mode)
    {
        var planner = _page.Services!.Planner;
        var legs = await Task.Run(() => planner.Plan(maps, mode)).ConfigureAwait(true);

        // A later change planned again: this answer is old.
        if (version != _version)
            return;

        _legs = legs;
        BuildSteps();
        BuildScene();
        SummarizeLegs();
    }

    private void SummarizeLegs()
    {
        var broken = _legs.Select((leg, index) => (leg, index)).Where(pair => pair.leg.Kind == RouteLegKind.Unreachable).ToList();
        var maps = new HashSet<long>(_anchors.Select(anchor => anchor.MapId));
        var localizer = _page.Localizer;

        foreach (var leg in _legs)
            maps.UnionWith(leg.Between);

        MapCount = maps.Count;
        GapCount = broken.Count;

        if (broken.Count == 0)
        {
            Problem = null;
            ProblemStep = -1;
        }
        else
        {
            var from = broken[0].index;
            var to = from + 1 >= _anchors.Count ? 0 : from + 1;

            Problem = localizer.Format("RouteProblemNone", from + 1, to + 1);
            ProblemStep = from;
        }
    }

    private void BuildSteps()
    {
        var services = _page.Services!;
        var localizer = _page.Localizer;
        var steps = new List<RouteStepViewModel>(_anchors.Count);

        for (var i = 0; i < _anchors.Count; i++)
        {
            var anchor = _anchors[i];
            var elsewhere = services.Index.TryPlace(anchor.MapId, out var layer, out _, out _) && layer != Layer;
            var leg = i < _legs.Count ? _legs[i] : null;
            var closing = i == _anchors.Count - 1;
            string gap = "";
            var broken = false;

            if (leg is not null && (!closing || RoutePlanner.ClosesBack(Mode, _anchors.Count)))
            {
                (gap, broken) = leg.Kind switch
                {
                    RouteLegKind.Unreachable => (localizer["RouteGapNone"], true),
                    RouteLegKind.Same => (localizer["RouteGapSame"], false),
                    _ when leg.UsesZaap => (localizer["RouteGapZaap"], false),
                    _ => (leg.Between.IsEmpty ? localizer["RouteGapNeighbours"] : localizer.Plural("RouteGapMaps", leg.Between.Length), false),
                };

                if (closing && !broken)
                    gap = localizer.Format("RouteGapBack", gap);
                else if (closing)
                    gap = localizer["RouteGapNone"];
            }

            steps.Add(new RouteStepViewModel(i, anchor.MapId, Place(anchor.MapId), Zone(anchor.MapId), anchor.IsPinned, elsewhere, gap, broken, closing && gap.Length > 0, anchor.Actions.Length, anchor.Actions.Length == 0 ? "" : localizer.Plural("RouteActionsCount", anchor.Actions.Length), Reveal, Remove, OpenActionPicker) { IsSelected = i == SelectedIndex });
        }

        Steps.Clear();

        foreach (var step in steps)
            Steps.Add(step);

        OnPropertyChanged(nameof(IsEmpty));
    }

    private void MarkSelected()
    {
        foreach (var step in Steps)
            step.IsSelected = step.Index == SelectedIndex;
    }

    private void BuildScene()
    {
        var index = _page.Services!.Index;
        var stations = new List<WorldMapStation>();
        var legs = new List<WorldMapLeg>();

        _stationAnchors.Clear();
        _sceneLegs.Clear();

        for (var i = 0; i < _anchors.Count; i++)
        {
            var anchor = _anchors[i];

            if (!index.TryPlace(anchor.MapId, out var layer, out var x, out var y) || layer != Layer)
                continue;

            stations.Add(new WorldMapStation(x, y, Zone(anchor.MapId), anchor.IsPinned, i == SelectedIndex));
            _stationAnchors.Add(i);
        }

        for (var j = 0; j < _legs.Count && j < _anchors.Count; j++)
        {
            var leg = _legs[j];

            if (leg.Kind == RouteLegKind.Same || !index.TryPlace(leg.From, out var fromLayer, out var fx, out var fy) || !index.TryPlace(leg.To, out var toLayer, out var tx, out var ty) || fromLayer != Layer || toLayer != Layer)
                continue;

            List<(int X, int Y)> squares = [(fx, fy)];

            foreach (var between in leg.Between)
            {
                if (index.TryPlace(between, out var layer, out var bx, out var by) && layer == Layer && !Contains(squares, bx, by))
                    squares.Add((bx, by));
            }

            if (!Contains(squares, tx, ty))
                squares.Add((tx, ty));

            if (squares.Count < 2)
                continue;

            legs.Add(new WorldMapLeg(squares, leg.Kind == RouteLegKind.Unreachable ? WorldMapLegKind.Unreachable : WorldMapLegKind.Path, j == _anchors.Count - 1));
            _sceneLegs.Add(j);
        }

        Scene = new WorldMapScene(stations, legs);
    }

    partial void OnToolChanged(WorldMapTool value)
    {
        OnPropertyChanged(nameof(IsTracing));
        OnPropertyChanged(nameof(IsSelecting));
    }

    partial void OnAddTextChanged(string value)
    {
        AddFailed = false;
    }

    partial void OnChoiceChanged(SharedChoiceViewModel? value)
    {
        OnPropertyChanged(nameof(HasChoice));
    }

    partial void OnProblemChanged(string? value)
    {
        OnPropertyChanged(nameof(HasProblem));
    }

    partial void OnSelectedIndexChanged(int value)
    {
        MarkSelected();
        BuildActions();
    }

    partial void OnLayerChanged(int value)
    {
        BuildSteps();
        BuildScene();
    }

    private readonly record struct Snapshot(ImmutableArray<RouteAnchor> Anchors, RouteMode Mode, RouteUnreachable Unreachable)
    {
        public bool Equals(Snapshot other)
        {
            return Mode == other.Mode && Unreachable == other.Unreachable && Anchors.AsSpan().SequenceEqual(other.Anchors.AsSpan());
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Anchors.Length, Mode, Unreachable);
        }
    }
}
