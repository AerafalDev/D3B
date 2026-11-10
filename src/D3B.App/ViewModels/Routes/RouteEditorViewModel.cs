using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Controls.WorldMaps;
using D3B.App.ViewModels.Studio;
using D3B.Studio.Assets;

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
    private readonly RoutesViewModel _page;
    private readonly RouteTexts _texts;
    private readonly List<RouteAnchor> _anchors;
    private readonly List<int> _stationAnchors = [];
    private readonly List<int> _sceneLegs = [];
    private readonly RouteHistory _history;
    private readonly RoutePlanning _planning = new();
    private readonly RouteTrace _trace = new();
    private readonly RouteActionList _actions;
    private RouteAsset _saved;
    private RouteStepSelection _chosen = RouteStepSelection.None;

    /// <summary>Gets the route's id.</summary>
    public string Id => _saved.Id;

    /// <summary>Gets the name of the route's tab.</summary>
    public string TabKey => _saved.Id;

    /// <summary>Gets the route's anchors as they are now, changes not saved included, for the world's map to draw the route by while another is edited.</summary>
    public IReadOnlyList<RouteAnchor> CurrentAnchors => _anchors;

    /// <summary>Gets the route's anchors as steps, in order.</summary>
    public ObservableCollection<RouteStepViewModel> Steps { get; } = [];

    /// <summary>Gets the actions of the step chosen, in order (D377).</summary>
    public ObservableCollection<RouteActionViewModel> Actions => _actions.Rows;

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

    /// <summary>Gets the route's steps set as clips on tracks (D395, D396).</summary>
    public SequencerViewModel Sequencer { get; }

    /// <summary>Gets the steps selected, in order: several once Control or Shift chose more than one (D395); <see cref="SelectedIndex"/> is the one chosen last.</summary>
    public IReadOnlyList<int> SelectedSteps => _chosen.Steps;

    /// <summary>Gets whether more than one step is selected.</summary>
    public bool HasSeveralSteps => _chosen.IsSeveral;

    /// <summary>Gets whether one step alone is selected, whose own actions the Details list.</summary>
    public bool HasSingleStep => HasSelectedStep && !HasSeveralSteps;

    /// <summary>Gets the title of the chosen step's actions, such as "Actions on step 3".</summary>
    public string ActionsTitle => HasSelectedStep ? _page.Localizer.Format("RouteActionsTitle", SelectedIndex + 1) : "";

    /// <summary>Gets whether the chosen step has no action.</summary>
    public bool HasNoActions => HasSelectedStep && Actions.Count == 0;

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
    public bool CanUndo => _history.CanUndo;

    /// <inheritdoc/>
    public bool CanRedo => _history.CanRedo;

    /// <summary>Gets what the path search has not finished.</summary>
    public Task Planned => _planning.Planned;

    /// <summary>Initializes a new instance of the <see cref="RouteEditorViewModel"/> class.</summary>
    /// <param name="page">The routes, which hold what the editors read.</param>
    /// <param name="route">The route.</param>
    public RouteEditorViewModel(RoutesViewModel page, RouteAsset route)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(route);

        _page = page;
        _texts = new RouteTexts(page);
        _saved = route;
        _anchors = [.. route.Anchors];
        _actions = new RouteActionList(page, _anchors, () => SelectedIndex, Changed, NotifyActions);

        var services = page.Services ?? throw new InvalidOperationException("The routes are edited once the host runs.");

        Source = new WorldMapSource(services.Catalog, services.Index, services.ReadTile);
        Name = route.Name;
        Mode = route.Mode;
        Unreachable = route.Unreachable;
        _history = new RouteHistory(Take());
        Hud = page.Localizer["RouteHudNothing"];
        Layer = FirstLayer();
        Sequencer = new SequencerViewModel(this, page);
        Relabel();
        Rebuild();
    }

    /// <summary>Raised when the route's steps were read again: they changed, or the language did.</summary>
    public event Action? Rebuilt;

    /// <summary>Raised when the map should show the coordinates of a step, such as when it is chosen from the list.</summary>
    public event Action<int, int>? RevealRequested;

    /// <summary>Raised when the map should show the whole route.</summary>
    public event Action? FitRequested;

    /// <summary>Says the worlds, the steps and the zones again, in the language chosen.</summary>
    public void Relabel()
    {
        var worlds = RouteWorldChoices.Build(_page, _texts, Layer, UseLayer);

        Worlds = worlds;
        LayerName = worlds.FirstOrDefault(world => world.Id == Layer)?.Name ?? _page.Localizer.Format("RouteWorldNumber", Layer);
        OnPropertyChanged(nameof(Worlds));
        OnPropertyChanged(nameof(HasWorlds));
        Name = _saved.Name;
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
            _history.Reset(Take());
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
            WorldMapHitKind.Square or WorldMapHitKind.Shared => _texts.Describe(Layer, hit.X, hit.Y),
            WorldMapHitKind.Station when hit.Station >= 0 && hit.Station < _stationAnchors.Count => _texts.StepHud(_anchors, _stationAnchors[hit.Station]),
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
    /// <param name="modifiers">The keys held: Control adds the step to the selection or takes it off, Shift selects up to it.</param>
    public void SelectStation(int station, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (station < 0 || station >= _stationAnchors.Count)
            return;

        var step = _stationAnchors[station];

        if (modifiers.HasFlag(KeyModifiers.Shift))
            ExtendTo(step);
        else if (modifiers.HasFlag(KeyModifiers.Control))
            ToggleStep(step);
        else
            Select(step);
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
        var map = _trace.Press(_page.Services!, Layer, x, y, isDrag, _anchors);

        if (map != 0)
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
        var map = _planning.Legs[leg].Between.FirstOrDefault(between => index.TryPlace(between, out var layer, out var bx, out var by) && layer == Layer && bx == x && by == y);

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
        var items = maps.Select(map => new MapChoiceViewModel(map, _texts.Zone(map), _texts.IsInside(map), _texts.IsInside(map) ? localizer["RouteSharedInside"] : localizer["RouteSharedOutside"])).ToList();

        Choice = new SharedChoiceViewModel(localizer.Format("RouteSharedTitle", maps.Length, localizer.Format("Coordinates", x, y)), items, at, Choose, CloseChoice);
    }

    /// <summary>Selects a step alone, the map showing it.</summary>
    /// <param name="index">The step's place in the route.</param>
    public void Select(int index)
    {
        Apply(RouteStepSelection.Only(index, _anchors.Count));
    }

    /// <summary>Adds a step to the selection, or takes it off: a click with Control.</summary>
    /// <param name="index">The step's place in the route.</param>
    public void ToggleStep(int index)
    {
        Apply(_chosen.Toggled(index, _anchors.Count));
    }

    /// <summary>Selects the steps from the one chosen first to a step: a click with Shift.</summary>
    /// <param name="index">The step's place in the route.</param>
    public void ExtendTo(int index)
    {
        Apply(_chosen.ExtendedTo(index, _anchors.Count));
    }

    /// <summary>Selects several steps, those of the route that are given.</summary>
    /// <param name="steps">The steps' places in the route, in any order.</param>
    /// <param name="active">The step chosen last, or -1 for the last of them.</param>
    public void SelectSteps(IEnumerable<int> steps, int active = -1)
    {
        Apply(RouteStepSelection.Of(steps, active, _anchors.Count));
    }

    /// <summary>Selects a step from the list, the map moving to show it.</summary>
    /// <param name="index">The step's place in the route.</param>
    public void Reveal(int index)
    {
        Select(index);
        ShowStep(index);
    }

    /// <summary>Moves the map to show a step, the selection left as it is.</summary>
    /// <param name="index">The step's place in the route.</param>
    public void ShowStep(int index)
    {
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

    /// <summary>Removes the steps selected, as one change: Delete.</summary>
    public void RemoveSelected()
    {
        if (!_chosen.IsSeveral)
        {
            Remove(SelectedIndex);

            return;
        }

        var first = _chosen.Steps[0];

        foreach (var step in _chosen.Steps.Reverse())
            _anchors.RemoveAt(step);

        Apply(RouteStepSelection.Only(Math.Min(first, _anchors.Count - 1), _anchors.Count));
        Changed();
    }

    /// <summary>Moves the step chosen in the route: Alt with the up and down arrows.</summary>
    /// <param name="delta">How many places towards the end, negative towards the start.</param>
    public void MoveSelected(int delta)
    {
        MoveStep(SelectedIndex, SelectedIndex + delta);
    }

    /// <summary>Moves a step to another place in the route, and chooses it: a drag in the inspector's list.</summary>
    /// <param name="from">The step's place now.</param>
    /// <param name="to">The place it takes.</param>
    public void MoveStep(int from, int to)
    {
        if (from < 0 || from >= _anchors.Count || to < 0 || to >= _anchors.Count || to == from)
            return;

        var anchor = _anchors[from];

        _anchors.RemoveAt(from);
        _anchors.Insert(to, anchor);
        SelectedIndex = to;
        Changed();
    }

    [RelayCommand]
    private void ReorderSteps(ReorderRequest request)
    {
        MoveStep(request.From, request.To);
    }

    [RelayCommand]
    private void ReorderActions(ReorderRequest request)
    {
        _actions.Move(request.From, request.To - request.From);
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
        _history.BeginGesture();
    }

    /// <summary>Ends the gesture.</summary>
    public void EndGesture()
    {
        _history.EndGesture();
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _history.Undo(Restore);
    }

    /// <inheritdoc/>
    public void Redo()
    {
        _history.Redo(Restore);
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

        if (RouteTypedAnchor.TryCoordinates(text, out var x, out var y))
        {
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

        if (RouteTypedAnchor.TryMapId(text, out var map) && services.Index.TryPlace(map, out var layer, out _, out _))
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

    [RelayCommand]
    private void RemoveAction(int index)
    {
        _actions.Remove(index);
    }

    private void NotifyActions()
    {
        OnPropertyChanged(nameof(HasSelectedStep));
        OnPropertyChanged(nameof(HasSingleStep));
        OnPropertyChanged(nameof(ActionsTitle));
        OnPropertyChanged(nameof(HasNoActions));
    }

    /// <summary>Replaces the steps by others of the same maps, as one change: the Sequencer's gestures, a clip drawn, moved or taken off.</summary>
    /// <param name="anchors">The route's steps after the edit, the same number as before.</param>
    public void ReplaceAnchors(IReadOnlyList<RouteAnchor> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        _anchors.Clear();
        _anchors.AddRange(anchors);
        Changed();
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
        if (_history.Record(Take(), out var before))
        {
            if (!before.Anchors.Select(anchor => anchor.MapId).SequenceEqual(_anchors.Select(anchor => anchor.MapId)))
                Apply(RouteStepSelection.Only(SelectedIndex, _anchors.Count));

            Moved();
        }

        Rebuild();
        RecomputeDirty();
    }

    private RouteSnapshot Take()
    {
        return new RouteSnapshot([.. _anchors], Mode, Unreachable);
    }

    private void Restore(RouteSnapshot state)
    {
        _anchors.Clear();
        _anchors.AddRange(state.Anchors);
        Mode = state.Mode;
        Unreachable = state.Unreachable;
        _history.Adopt(state);
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

    private void Apply(RouteStepSelection selection)
    {
        _chosen = selection;

        if (SelectedIndex != selection.Active)
            SelectedIndex = selection.Active;

        OnPropertyChanged(nameof(SelectedSteps));
        OnPropertyChanged(nameof(HasSeveralSteps));
        OnPropertyChanged(nameof(HasSingleStep));
        MarkSelected();
        BuildScene();
    }

    private void Rebuild()
    {
        if (!ReferenceEquals(_chosen.Pruned(_anchors.Count), _chosen))
            Apply(_chosen.Pruned(_anchors.Count));

        StepCount = _anchors.Count;
        ActionTotal = _anchors.Sum(anchor => anchor.Actions.Length);
        OnPropertyChanged(nameof(IsEmpty));
        BuildSteps();
        BuildScene();

        if (!_actions.KeepsForms)
            _actions.Build();

        _planning.Start(_page.Services!.Planner, _anchors.ConvertAll(anchor => anchor.MapId), Mode, OnPlanned);
        Rebuilt?.Invoke();
    }

    private void OnPlanned()
    {
        BuildSteps();
        BuildScene();

        var summary = RouteLegSummary.Of(_anchors, _planning.Legs, _page.Localizer);

        MapCount = summary.MapCount;
        GapCount = summary.GapCount;
        Problem = summary.Problem;
        ProblemStep = summary.ProblemStep;
    }

    private void BuildSteps()
    {
        var steps = RouteStepRows.Build(_page.Services!, _page.Localizer, _texts, _anchors, _planning.Legs, Mode, Layer, _chosen.Steps, Reveal, Remove);

        Steps.Clear();

        foreach (var step in steps)
            Steps.Add(step);

        OnPropertyChanged(nameof(IsEmpty));
    }

    private void MarkSelected()
    {
        foreach (var step in Steps)
            step.IsSelected = _chosen.Steps.Contains(step.Index);
    }

    private void BuildScene()
    {
        var built = RouteSceneBuilder.Build(_page.Services!, _texts, _anchors, _planning.Legs, Layer, _chosen.Steps);

        _stationAnchors.Clear();
        _stationAnchors.AddRange(built.StationAnchors);
        _sceneLegs.Clear();
        _sceneLegs.AddRange(built.SceneLegs);
        Scene = built.Scene;
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
        if (_chosen.Active != value)
            _chosen = RouteStepSelection.Only(value, _anchors.Count);

        OnPropertyChanged(nameof(SelectedSteps));
        OnPropertyChanged(nameof(HasSeveralSteps));
        OnPropertyChanged(nameof(HasSingleStep));
        MarkSelected();
        _actions.Build();
    }

    partial void OnLayerChanged(int value)
    {
        BuildSteps();
        BuildScene();
    }
}
