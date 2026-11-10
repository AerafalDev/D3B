using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Zones;
using D3B.Data;
using D3B.Data.WorldMaps;
using D3B.Studio.Assets;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.World;

/// <summary>The studio's centre: one persistent viewport on the world, where characters live, routes and zones lie, and what is selected is edited (D395, D396, D397).</summary>
/// <remarks>
/// <para>
/// The world's map (D372) is drawn once and stays: selecting a route or a zone, from the map or the Outliner, gives it the map's
/// gestures through its editor, and the other routes, zones and characters stay drawn around it. The editors keep their
/// models (anchors, path, undo, the keys of D375); the tab per route and per zone is gone, the map being theirs together.
/// </para>
/// <para>
/// The toolbar's tools are the viewport's: Select presses and moves what is there, Route traces the selected route, or makes one
/// on the first press, Zone paints the selected zone likewise, and Record waits for 7d4b.
/// </para>
/// </remarks>
internal sealed partial class WorldViewportViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly RoutesViewModel _routes;
    private readonly ZonesViewModel _zones;
    private readonly WorldSelection _selection;
    private readonly Func<IEnumerable<WorldCharacterSnapshot>> _characters;
    private readonly Func<string?> _framed;
    private readonly FollowedRuns _followed;
    private readonly HashSet<string> _hidden = [];
    private readonly Dictionary<string, (ZoneAsset Zone, int Layer, IReadOnlyList<(int X, int Y)> Squares)> _zoneSquares = new(StringComparer.Ordinal);
    private WorldMapSource? _source;
    private string _signature = "";
    private bool _fromMap;
    private int _lit = -1;

    /// <summary>Gets where the map reads its worlds and tiles from, once the host runs.</summary>
    public WorldMapSource? Source
    {
        get => _source;
        private set
        {
            if (SetProperty(ref _source, value))
                OnPropertyChanged(nameof(IsAvailable));
        }
    }

    /// <summary>Gets whether the map can be drawn: the host runs and holds the world's maps.</summary>
    public bool IsAvailable => _source is not null;

    /// <summary>Gets the points of interest the map draws, and the ticks that choose which (D414).</summary>
    public WorldHintsViewModel Hints { get; }

    /// <summary>Gets the worlds the layer can show.</summary>
    public IReadOnlyList<WorldChoiceViewModel> Worlds { get; private set; } = [];

    /// <summary>Gets whether there is a world to choose besides the one shown.</summary>
    public bool HasWorlds => Worlds.Count > 1;

    /// <summary>Gets the route selected, whose editor takes the map's gestures, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoute))]
    public partial RouteEditorViewModel? Route { get; private set; }

    /// <summary>Gets the zone selected, whose editor takes the map's gestures, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasZone))]
    public partial ZoneEditorViewModel? Zone { get; private set; }

    /// <summary>Gets what the map draws: the edited route or zone, and what else the world holds.</summary>
    [ObservableProperty]
    public partial WorldMapScene Scene { get; private set; } = WorldMapScene.Empty;

    /// <summary>Gets the world shown, one layer at a time (D299).</summary>
    [ObservableProperty]
    public partial int Layer { get; private set; }

    /// <summary>Gets the name of the world shown.</summary>
    [ObservableProperty]
    public partial string LayerName { get; private set; } = "";

    /// <summary>Gets or sets the tool chosen in the toolbar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelecting), nameof(IsRouting), nameof(IsZoning), nameof(ShowsZoneTools), nameof(ShowsAdd))]
    public partial WorldTool Tool { get; set; }

    /// <summary>Gets or sets the zone tool chosen while the Zone tool is: paint, erase or a whole sub-area.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPainting), nameof(IsErasing), nameof(IsChoosingSubAreas))]
    public partial WorldMapTool ZoneTool { get; set; } = WorldMapTool.Trace;

    /// <summary>Gets what a press on the map does, as the tool and the selection ask.</summary>
    [ObservableProperty]
    public partial WorldMapTool MapTool { get; private set; }

    /// <summary>Gets or sets whether the outline of each map shows.</summary>
    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    /// <summary>Gets or sets whether the mark of coordinates that several maps share shows.</summary>
    [ObservableProperty]
    public partial bool ShowShared { get; set; } = true;

    /// <summary>Gets what lies under the pointer, in words.</summary>
    [ObservableProperty]
    public partial string Hud { get; private set; }

    /// <summary>Gets the zoom, as a percentage.</summary>
    [ObservableProperty]
    public partial string ZoomText { get; private set; } = "";

    /// <summary>Gets what the viewport says while a character plays: its frame is green and this is its caption, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFramed))]
    public partial string? FrameText { get; private set; }

    /// <summary>Gets whether a route is selected.</summary>
    public bool HasRoute => Route is not null;

    /// <summary>Gets whether a zone is selected.</summary>
    public bool HasZone => Zone is not null;

    /// <summary>Gets whether the chosen character plays: the viewport's frame is green (Play in Editor, D395).</summary>
    public bool IsFramed => FrameText is not null;

    /// <summary>Gets the choice among maps that share coordinates, while the route selected has one open.</summary>
    public SharedChoiceViewModel? Choice => Route?.Choice;

    /// <summary>Gets whether a choice among shared maps is open.</summary>
    public bool HasChoice => Route?.Choice is not null;

    /// <summary>Gets whether the zone selected holds sub-areas, which its panel lists.</summary>
    public bool ShowsSubAreas => Zone is { HasSubAreas: true };

    /// <summary>Gets whether the route's typed id shows: the Route tool is in use.</summary>
    public bool ShowsAdd => IsRouting && Route is not null;

    /// <summary>Gets whether the Select tool is chosen.</summary>
    public bool IsSelecting => Tool is WorldTool.Select;

    /// <summary>Gets whether the Route tool is chosen.</summary>
    public bool IsRouting => Tool is WorldTool.Route;

    /// <summary>Gets whether the Zone tool is chosen.</summary>
    public bool IsZoning => Tool is WorldTool.Zone;

    /// <summary>Gets whether the zone's own tools show under the toolbar.</summary>
    public bool ShowsZoneTools => IsZoning && Zone is not null;

    /// <summary>Gets whether the zone tool paints.</summary>
    public bool IsPainting => ZoneTool is WorldMapTool.Trace;

    /// <summary>Gets whether the zone tool erases.</summary>
    public bool IsErasing => ZoneTool is WorldMapTool.Erase;

    /// <summary>Gets whether the zone tool takes whole sub-areas.</summary>
    public bool IsChoosingSubAreas => ZoneTool is WorldMapTool.Region;

    /// <summary>Gets what the last tool started to make, a route or a zone, which has not finished.</summary>
    public Task Creating { get; private set; } = Task.CompletedTask;

    /// <summary>Initializes a new instance of the <see cref="WorldViewportViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="routes">The routes, whose editors take the map's gestures.</param>
    /// <param name="zones">The zones, likewise.</param>
    /// <param name="selection">What is selected in the world.</param>
    /// <param name="characters">The characters online, whose places the map shows.</param>
    /// <param name="framed">Says the name of the character that plays, or <see langword="null"/> when none does.</param>
    /// <param name="followed">The characters that follow a route now, which the map marks, or <see langword="null"/> for none (D419).</param>
    public WorldViewportViewModel(Localizer localizer, RoutesViewModel routes, ZonesViewModel zones, WorldSelection selection, Func<IEnumerable<WorldCharacterSnapshot>> characters, Func<string?> framed, FollowedRuns? followed = null)
    {
        _followed = followed ?? FollowedRuns.None;
        _localizer = localizer;
        _routes = routes;
        _zones = zones;
        _selection = selection;
        _characters = characters;
        _framed = framed;
        Hints = new WorldHintsViewModel(localizer);
        Hud = localizer["WorldHudNothing"];
        selection.Changed += OnSelectionChanged;
    }

    /// <summary>Raised when the map should show the coordinates of a map, such as when a step or a character is chosen from a list.</summary>
    public event Action<int, int>? RevealRequested;

    /// <summary>Raised when the map should show the whole route or zone.</summary>
    public event Action? FitRequested;

    private static int FirstLayer(RouteServices services)
    {
        return services.Catalog.TryGet(1, out _) ? 1 : services.Catalog.Worlds.Span[0].Id;
    }

    private static Color ColorOf(IBrush? brush)
    {
        return brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;
    }

    /// <summary>Tells whether the eye of a route, a zone or a character is closed in the Outliner.</summary>
    /// <param name="key">What the Outliner names it by: <c>route:</c>, <c>zone:</c> or <c>character:</c> and its id.</param>
    /// <returns>Whether the map leaves it out.</returns>
    public bool IsHidden(string key)
    {
        return _hidden.Contains(key);
    }

    /// <summary>Opens or closes the eye of a route, a zone or a character: the map shows it or leaves it out.</summary>
    /// <param name="key">What the Outliner names it by.</param>
    public void ToggleVisible(string key)
    {
        if (!_hidden.Remove(key))
            _hidden.Add(key);

        Compose();
    }

    /// <summary>Reads the host again: the map's source once it runs, the project and the characters' places as they change.</summary>
    public void Refresh()
    {
        if (Source is null && _routes.Services is { } services)
        {
            Source = new WorldMapSource(services.Catalog, services.Index, services.ReadTile);
            Layer = FirstLayer(services);
            Hints.Use(services.Provider?.GetService<GameTables>(), services.Texts);
            Hints.UseLayer(Layer);
            Relabel();
            Compose();
        }

        if (_routes.Services is not { } read)
            return;

        FrameText = _framed() is { } name ? FrameSaid(name) : null;

        var signature = Signature(read);

        if (signature == _signature)
            return;

        _signature = signature;
        Compose();
    }

    /// <summary>Says the worlds and the maps' words again, in the language chosen.</summary>
    public void Relabel()
    {
        Hud = _localizer["WorldHudNothing"];
        Hints.Relabel();

        if (_routes.Services is not { } services)
            return;

        var worlds = new List<WorldChoiceViewModel>();

        foreach (var world in services.Catalog.Worlds.Span)
        {
            var count = services.Index.SquaresOf(world.Id).ToArray().Sum(square => square.Count);

            if (count > 0)
                worlds.Add(new WorldChoiceViewModel(world.Id, WorldName(services, world), count, _localizer.Plural("RouteWorldMaps", count), world.Id == Layer, UseLayer));
        }

        Worlds = worlds;
        LayerName = worlds.FirstOrDefault(world => world.Id == Layer)?.Name ?? _localizer.Format("RouteWorldNumber", Layer);
        OnPropertyChanged(nameof(Worlds));
        OnPropertyChanged(nameof(HasWorlds));
    }

    /// <summary>Shows another world, for the viewport and for the editor that has the gestures.</summary>
    /// <param name="world">The world's id.</param>
    public void UseLayer(int world)
    {
        if (world == Layer)
            return;

        Layer = world;
        Hints.UseLayer(world);
        Route?.UseLayer(world);
        Zone?.UseLayer(world);
        Relabel();
        Compose();
    }

    /// <summary>Chooses a tool of the toolbar, as its button does.</summary>
    /// <param name="tool">The tool.</param>
    [RelayCommand]
    public void SetTool(WorldTool tool)
    {
        switch (tool)
        {
            case WorldTool.Select:
                Tool = WorldTool.Select;

                break;
            case WorldTool.Route when Route is not null:
                Tool = WorldTool.Route;

                break;
            case WorldTool.Route when _routes.IsAvailable:
                Creating = StartRouteAsync();

                break;
            case WorldTool.Zone when Zone is not null:
                Tool = WorldTool.Zone;

                break;
            case WorldTool.Zone when _zones.IsAvailable:
                Creating = StartZoneAsync();

                break;
        }
    }

    /// <summary>Chooses what the Zone tool does: paint, erase or take a whole sub-area.</summary>
    /// <param name="tool">The tool.</param>
    [RelayCommand]
    public void SetZoneTool(WorldMapTool tool)
    {
        ZoneTool = tool;

        if (Zone is not null)
            Tool = WorldTool.Zone;

        ApplyTool();
    }

    /// <summary>Chooses a step from the map: its node was pressed.</summary>
    /// <param name="station">The station's place in the scene.</param>
    /// <param name="modifiers">The keys held: Control and Shift choose several steps.</param>
    public void SelectStation(int station, KeyModifiers modifiers = KeyModifiers.None)
    {
        Route?.SelectStation(station, modifiers);
    }

    /// <summary>Moves a step onto other coordinates, while it is dragged.</summary>
    /// <param name="station">The station's place in the scene.</param>
    /// <param name="x">The coordinates dragged onto.</param>
    /// <param name="y">The coordinates dragged onto.</param>
    public void MoveStation(int station, int x, int y)
    {
        Route?.MoveStation(station, x, y);
    }

    /// <summary>Starts a gesture on the map, which one change of the editor is.</summary>
    public void BeginGesture()
    {
        Route?.BeginGesture();
        Zone?.BeginGesture();
    }

    /// <summary>Ends the gesture.</summary>
    public void EndGesture()
    {
        Route?.EndGesture();
        Zone?.EndGesture();
    }

    /// <summary>Gives a press of the trace tool, or each square a drag crosses, to the route or the zone that has the gestures.</summary>
    /// <param name="x">The coordinates.</param>
    /// <param name="y">The coordinates.</param>
    /// <param name="isDrag">Whether a drag crossed the coordinates.</param>
    public void TraceSquare(int x, int y, bool isDrag)
    {
        if (Tool is WorldTool.Route)
            Route?.TraceSquare(x, y, isDrag);
        else if (Tool is WorldTool.Zone)
            Zone?.TraceSquare(x, y, isDrag);
    }

    /// <summary>Pins a map of the path, which becomes a step of the route.</summary>
    /// <param name="leg">The leg's place in the scene.</param>
    /// <param name="x">The map's coordinates.</param>
    /// <param name="y">The map's coordinates.</param>
    public void PinPathPoint(int leg, int x, int y)
    {
        Route?.PinPathPoint(leg, x, y);
    }

    /// <summary>Opens the choice among the maps that share coordinates, for the route.</summary>
    /// <param name="x">The coordinates.</param>
    /// <param name="y">The coordinates.</param>
    /// <param name="at">The point of the view the choice opens at.</param>
    public void OpenChoice(int x, int y, Point at)
    {
        Route?.OpenChoice(x, y, at);
    }

    /// <summary>Selects what was pressed: a character, a step of another route, a zone.</summary>
    /// <param name="hit">What lies under the pointer.</param>
    public void Press(WorldMapHit hit)
    {
        _fromMap = true;

        try
        {
            switch (hit.Kind)
            {
                case WorldMapHitKind.Character when CharacterKeys.Parse(hit.Id) is { } key:
                    _selection.SelectCharacter(key);

                    break;
                case WorldMapHitKind.RouteStep when hit.Id is { } route:
                    _selection.SelectRoute(route, hit.Step);

                    break;
                case WorldMapHitKind.Zone when hit.Id is { } zone:
                    _selection.SelectZone(zone);

                    break;
            }
        }
        finally
        {
            _fromMap = false;
        }
    }

    /// <summary>Says what is under the pointer on the map.</summary>
    /// <param name="hit">What it is.</param>
    public void Hover(WorldMapHit hit)
    {
        switch (hit.Kind)
        {
            case WorldMapHitKind.Hint:
                Hud = Hints.Describe(hit) ?? _localizer["WorldHudNothing"];

                return;
            case WorldMapHitKind.Character:
                Hud = Scene.Backdrop?.Characters.FirstOrDefault(character => character.Key == hit.Id) is { } character
                    ? _localizer.Format("WorldHudCharacter", character.Name, _localizer.Format("Coordinates", character.X, character.Y))
                    : _localizer["WorldHudNothing"];

                return;
            case WorldMapHitKind.RouteStep:
                Hud = Scene.Backdrop?.Routes.FirstOrDefault(route => route.Id == hit.Id) is { } route
                    ? _localizer.Format("WorldHudRouteStep", route.Name, hit.Step + 1, _localizer.Format("Coordinates", hit.X, hit.Y))
                    : _localizer["WorldHudNothing"];

                return;
            case WorldMapHitKind.Zone:
                Hud = Scene.Backdrop?.Zones.FirstOrDefault(zone => zone.Id == hit.Id) is { } zone
                    ? _localizer.Format("WorldHudZone", zone.Name, _localizer.Format("Coordinates", hit.X, hit.Y))
                    : _localizer["WorldHudNothing"];

                return;
        }

        if (Route is { } editor && Tool is not WorldTool.Zone)
        {
            editor.Hover(hit);
            Hud = editor.Hud;
        }
        else if (Zone is { } zoneEditor)
        {
            zoneEditor.Hover(hit);
            Hud = zoneEditor.Hud;
        }
        else
        {
            Hud = hit.Kind is WorldMapHitKind.Square or WorldMapHitKind.Shared ? _localizer.Format("Coordinates", hit.X, hit.Y) : _localizer["WorldHudNothing"];
        }
    }

    /// <summary>Says the zoom.</summary>
    /// <param name="percent">The zoom, as a percentage of the client's full size.</param>
    public void Zoomed(double percent)
    {
        ZoomText = percent.ToString("N0", _localizer.Culture) + " %";
        Route?.Zoomed(percent);
        Zone?.Zoomed(percent);
    }

    /// <summary>Removes the step selected, as Delete does.</summary>
    public void RemoveSelectedStep()
    {
        Route?.RemoveSelected();
    }

    /// <summary>Moves the step selected in its route: Alt and the arrows.</summary>
    /// <param name="delta">-1 earlier, 1 later.</param>
    public void MoveSelectedStep(int delta)
    {
        Route?.MoveSelected(delta);
    }

    /// <summary>Chooses the previous or the next step of the route and shows it: Page up and down.</summary>
    /// <param name="delta">-1 previous, 1 next.</param>
    public void StepBy(int delta)
    {
        Route?.StepBy(delta);
    }

    /// <summary>Goes back to the Select tool, closing the choice among shared maps first: Escape.</summary>
    /// <returns>Whether something changed.</returns>
    public bool Escape()
    {
        if (Route is { HasChoice: true } route)
        {
            route.CloseChoiceCommand.Execute(null);

            return true;
        }

        if (Tool is WorldTool.Select)
            return false;

        Tool = WorldTool.Select;

        return true;
    }

    private string WorldName(RouteServices services, WorldMap world)
    {
        return services.Texts is { } texts && texts.TryGet(_localizer.GameLanguage, world.NameId, out var name)
            ? name
            : _localizer.Format("RouteWorldNumber", world.Id);
    }

    private string Signature(RouteServices services)
    {
        var text = new System.Text.StringBuilder();

        text.Append(services.Library.Version).Append('|').Append(Layer).Append('|').Append(_hidden.Count).Append('|');

        foreach (var character in _characters())
            text.Append(CharacterKeys.Text(character.Key)).Append(':').Append(character.MapId).Append(':').Append(character.Playing).Append(';');

        foreach (var run in _followed.All())
            text.Append(CharacterKeys.Text(run.Position.Character)).Append(':').Append(run.Position.Route).Append(':').Append(run.Position.Anchor).Append(run.Position.Arrived).Append(run.Position.Returning).Append(';');

        return text.ToString();
    }

    private async Task StartRouteAsync()
    {
        if (await _routes.CreateAsync("").ConfigureAwait(true) is { } route)
        {
            _selection.SelectRoute(route.Id);
            Tool = WorldTool.Route;
        }
    }

    private async Task StartZoneAsync()
    {
        if (await _zones.CreateAsync("").ConfigureAwait(true) is { } zone)
        {
            _selection.SelectZone(zone.Id);
            Tool = WorldTool.Zone;
        }
    }

    // The editor of what is selected has the map's gestures: the others stay drawn as they are saved.
    private void OnSelectionChanged()
    {
        var route = _selection.Kind is WorldSelectionKind.Route && _selection.AssetId is { } routeId ? _routes.Find(routeId) ?? _routes.OpenRoute(routeId) : null;
        var zone = _selection.Kind is WorldSelectionKind.Zone && _selection.AssetId is { } zoneId ? _zones.Find(zoneId) ?? _zones.OpenZone(zoneId) : null;
        var changed = route != Route || zone != Zone;

        SetActive(route, zone);

        if (route is not null && !_selection.Steps.IsEmpty && (route.SelectedIndex != _selection.Step || !route.SelectedSteps.SequenceEqual(_selection.Steps)))
            route.SelectSteps(_selection.Steps, _selection.Step);

        if (!_fromMap)
            Show(changed, route, zone);

        Compose();
    }

    // Chosen from a list, what is selected is brought into view: its world shown, the map put on it.
    private void Show(bool changed, RouteEditorViewModel? route, ZoneEditorViewModel? zone)
    {
        if (route is not null && changed)
        {
            FitRequested?.Invoke();
        }
        else if (route is not null && route.SelectedIndex >= 0)
        {
            route.ShowStep(route.SelectedIndex);
        }
        else if (zone is not null && changed)
        {
            FitRequested?.Invoke();
        }
        else if (_selection is { Kind: WorldSelectionKind.Character, Character: { } key } && _characters().FirstOrDefault(character => character.Key == key) is { } character && Place(character) is { } place)
        {
            if (place.Layer != Layer)
                UseLayer(place.Layer);

            RevealRequested?.Invoke(place.X, place.Y);
        }
    }

    private (int Layer, int X, int Y)? Place(WorldCharacterSnapshot character)
    {
        return _routes.Services is { } services && character.MapId is { } map && services.Index.TryPlace(map, out var layer, out var x, out var y) ? (layer, x, y) : null;
    }

    private void SetActive(RouteEditorViewModel? route, ZoneEditorViewModel? zone)
    {
        if (route != Route)
        {
            if (Route is { } old)
            {
                old.PropertyChanged -= OnRouteChanged;
                old.RevealRequested -= OnRevealRequested;
                old.FitRequested -= OnFitRequested;
                old.Sequencer.StepLit -= OnStepLit;
                old.Tool = WorldMapTool.Select;
            }

            Route = route;
            _lit = -1;

            if (route is not null)
            {
                route.PropertyChanged += OnRouteChanged;
                route.RevealRequested += OnRevealRequested;
                route.FitRequested += OnFitRequested;
                route.Sequencer.StepLit += OnStepLit;
                AdoptLayerOf(route.Layer);
            }
        }

        if (zone != Zone)
        {
            if (Zone is { } old)
            {
                old.PropertyChanged -= OnZoneChanged;
                old.SubAreas.CollectionChanged -= OnSubAreasChanged;
                old.FitRequested -= OnFitRequested;
                old.Tool = WorldMapTool.Select;
            }

            Zone = zone;

            if (zone is not null)
            {
                zone.PropertyChanged += OnZoneChanged;
                zone.SubAreas.CollectionChanged += OnSubAreasChanged;
                zone.FitRequested += OnFitRequested;
                AdoptLayerOf(zone.Layer);
            }
        }

        // A tool needs what it works on: the selection moving to something else leaves it.
        if ((Tool is WorldTool.Route && Route is null) || (Tool is WorldTool.Zone && Zone is null))
            Tool = WorldTool.Select;

        OnPropertyChanged(nameof(ShowsZoneTools));
        OnPropertyChanged(nameof(ShowsSubAreas));
        OnPropertyChanged(nameof(ShowsAdd));
        OnPropertyChanged(nameof(Choice));
        OnPropertyChanged(nameof(HasChoice));
        ApplyTool();
    }

    private void AdoptLayerOf(int layer)
    {
        if (layer == Layer)
            return;

        Layer = layer;
        Relabel();
    }

    private void ApplyTool()
    {
        MapTool = Tool switch
        {
            WorldTool.Route when Route is not null => WorldMapTool.Trace,
            WorldTool.Zone when Zone is not null => ZoneTool,
            _ => WorldMapTool.Select,
        };

        if (Route is { } route)
            route.Tool = Tool is WorldTool.Route ? WorldMapTool.Trace : WorldMapTool.Select;

        if (Zone is { } zone)
            zone.Tool = Tool is WorldTool.Zone ? ZoneTool : WorldMapTool.Select;
    }

    private void OnRouteChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RouteEditorViewModel.Scene):
                Compose();

                break;
            case nameof(RouteEditorViewModel.Choice):
                OnPropertyChanged(nameof(Choice));
                OnPropertyChanged(nameof(HasChoice));

                break;
            case nameof(RouteEditorViewModel.SelectedSteps) when Route is { } route:
                // The editor chose the step itself (a map traced, a station pressed): the pointer is on the map and the map must stay
                // under it. Only a step chosen from a list is brought into view, and the editor reveals it then.
                _fromMap = true;

                try
                {
                    _selection.SelectSteps(route.Id, route.SelectedSteps, route.SelectedIndex);
                }
                finally
                {
                    _fromMap = false;
                }

                break;
            case nameof(RouteEditorViewModel.Layer) when Route is { } route:
                AdoptLayerOf(route.Layer);
                Compose();

                break;
        }
    }

    private void OnZoneChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ZoneEditorViewModel.Scene):
                Compose();

                break;
            case nameof(ZoneEditorViewModel.Layer) when Zone is { } zone:
                AdoptLayerOf(zone.Layer);
                Compose();

                break;
        }
    }

    private void OnSubAreasChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowsSubAreas));
    }

    private void OnRevealRequested(int x, int y)
    {
        RevealRequested?.Invoke(x, y);
    }

    private void OnStepLit(int step)
    {
        _lit = step;
        Compose();
    }

    private void OnFitRequested()
    {
        FitRequested?.Invoke();
    }

    partial void OnToolChanged(WorldTool value)
    {
        ApplyTool();
    }

    private void Compose()
    {
        var baseScene = (Route?.Scene ?? Zone?.Scene ?? WorldMapScene.Empty) with { LitStep = Route is null ? -1 : _lit };

        Scene = _routes.Services is { } services ? baseScene with { Backdrop = BuildBackdrop(services) } : baseScene;
    }

    private WorldMapBackdrop BuildBackdrop(RouteServices services)
    {
        var index = services.Index;
        var routes = new List<WorldMapBackdropRoute>();
        var zones = new List<WorldMapBackdropZone>();
        var characters = new List<WorldMapCharacter>();

        foreach (var asset in services.Library.Assets)
        {
            var key = (asset.Kind == AssetKind.Route ? "route:" : "zone:") + asset.Id;

            if (_hidden.Contains(key))
                continue;

            if (asset is RouteAsset route && route.Id != Route?.Id)
            {
                var anchors = _routes.Find(route.Id)?.CurrentAnchors ?? route.Anchors;
                var steps = new List<WorldMapBackdropStep>();

                for (var i = 0; i < anchors.Count; i++)
                {
                    if (index.TryPlace(anchors[i].MapId, out var layer, out var x, out var y) && layer == Layer)
                        steps.Add(new WorldMapBackdropStep(x, y, i, RouteActionKinds.Of(anchors[i].Actions)));
                }

                if (steps.Count > 0)
                    routes.Add(new WorldMapBackdropRoute(route.Id, route.Name, steps, route.Mode is RouteMode.Loop, _selection.IsRoute(route.Id)));
            }
            else if (asset is ZoneAsset zone && zone.Id != Zone?.Id)
            {
                var squares = SquaresOf(services, _zones.Find(zone.Id) is { IsDirty: true } open ? open.Current : zone);

                if (squares.Count > 0)
                    zones.Add(new WorldMapBackdropZone(zone.Id, zone.Name, squares, _selection.IsZone(zone.Id)));
            }
        }

        foreach (var character in _characters())
        {
            if (Place(character) is not { } place || place.Layer != Layer || _hidden.Contains("character:" + CharacterKeys.Text(character.Key)))
                continue;

            characters.Add(new WorldMapCharacter(CharacterKeys.Text(character.Key), character.Name, character.Initials, ColorOf(character.Color), place.X, place.Y, _selection.IsCharacter(character.Key), character.Playing));
        }

        return new WorldMapBackdrop(routes, zones, characters) { Runs = RunsOf(services) };
    }

    private string FrameSaid(string name)
    {
        if (_followed.All().FirstOrDefault(run => run.Name == name)?.Position is not { } route)
            return _localizer.Format("WorldPlays", name);

        var said = _localizer.Format("WorldPlaysStep", name, route.Step, route.Anchors);

        if (route.Mode is RouteMode.Loop)
            said += _localizer.Format("CardRouteLap", route.Lap);
        else if (route.Returning)
            said += _localizer["CardRouteBack"];

        return said;
    }

    private List<WorldMapRun> RunsOf(RouteServices services)
    {
        var runs = new List<WorldMapRun>();

        foreach (var run in _followed.All())
        {
            var position = run.Position;

            if (_hidden.Contains("route:" + position.Route) || _hidden.Contains("character:" + CharacterKeys.Text(position.Character)))
                continue;

            IReadOnlyList<RouteAnchor>? anchors = _routes.Find(position.Route)?.CurrentAnchors ?? (IReadOnlyList<RouteAnchor>?)services.Library.Find<RouteAsset>(position.Route)?.Anchors;

            if (anchors is null || position.Anchor >= anchors.Count || !services.Index.TryPlace(anchors[position.Anchor].MapId, out var layer, out var x, out var y) || layer != Layer)
                continue;

            int? fromX = null;
            int? fromY = null;

            // The leg being walked comes from the step before, which for a loop's first is its last and for a route going back is the one after.
            var from = position.Returning ? position.Anchor + 1 : position.Anchor > 0 ? position.Anchor - 1 : position.Mode is RouteMode.Loop ? anchors.Count - 1 : -1;

            if (from >= 0 && from < anchors.Count && from != position.Anchor && services.Index.TryPlace(anchors[from].MapId, out var fromLayer, out var px, out var py) && fromLayer == Layer)
            {
                fromX = px;
                fromY = py;
            }

            runs.Add(new WorldMapRun(CharacterKeys.Text(position.Character), run.Initials, ColorOf(run.Color), x, y, fromX, fromY, position.Arrived));
        }

        return runs;
    }

    private IReadOnlyList<(int X, int Y)> SquaresOf(RouteServices services, ZoneAsset zone)
    {
        if (_zoneSquares.TryGetValue(zone.Id, out var kept) && ReferenceEquals(kept.Zone, zone) && kept.Layer == Layer)
            return kept.Squares;

        var squares = new List<(int X, int Y)>();
        var seen = new HashSet<(int X, int Y)>();

        foreach (var map in ZoneMaps.Resolve(zone, services.Atlas))
        {
            if (services.Index.TryPlace(map, out var layer, out var x, out var y) && layer == Layer && seen.Add((x, y)))
                squares.Add((x, y));
        }

        _zoneSquares[zone.Id] = (zone, Layer, squares);

        return squares;
    }
}
