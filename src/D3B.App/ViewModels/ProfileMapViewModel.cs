using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Settings;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels;

/// <summary>The world's map beside a profile: the project's zones tinted and the place the settings name outlined, where a click picks the place (D415).</summary>
/// <remarks>
/// It reads the editor's settings and writes through its fields, so that the form and the map are one thing seen twice. Nothing is
/// painted here: painting a zone stays in the world's Zone tool, which the button below the map opens on the zone picked.
/// </remarks>
internal sealed partial class ProfileMapViewModel : ObservableObject
{
    private const string Automatic = "Automatic";
    private const string SubAreas = "Chosen";
    private const string Zone = "Zone";

    private readonly Localizer _localizer;
    private readonly Func<RouteServices?> _services;
    private readonly Action<string> _editZone;
    private SettingsEditor? _editor;
    private bool _fit;

    /// <summary>Gets the editor whose settings the map shows and picks.</summary>
    public SettingsEditor? Editor => _editor;

    /// <summary>Gets where the maps and their tiles are read from, once the host runs.</summary>
    [ObservableProperty]
    public partial WorldMapSource? Source { get; private set; }

    /// <summary>Gets the world shown: the one the place picked lies in, else the first.</summary>
    [ObservableProperty]
    public partial int Layer { get; private set; }

    /// <summary>Gets what the map draws: the project's zones as a backdrop, and the place the settings name as the outlined squares.</summary>
    [ObservableProperty]
    public partial WorldMapScene Scene { get; private set; } = WorldMapScene.Empty;

    /// <summary>Gets whether the host runs, so that there is a map to show.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>Gets what a click on the map does for the place chosen, in words.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    public partial string Hint { get; private set; } = "";

    /// <summary>Gets whether the map says what a click does.</summary>
    public bool HasHint => Hint.Length > 0;

    /// <summary>Gets the name of the zone or sub-area under the pointer, if one is.</summary>
    [ObservableProperty]
    public partial string Hovered { get; private set; } = "";

    /// <summary>Gets whether the place is a zone of the project, which the world's Zone tool can edit.</summary>
    [ObservableProperty]
    public partial bool CanEditZone { get; private set; }

    /// <summary>Gets the zone picked, if the place is a zone of the project.</summary>
    public string? PickedZone { get; private set; }

    /// <summary>Occurs when the map is to be put on what the settings name, as the editor opens or the place changes.</summary>
    public event Action? FitRequested;

    /// <summary>Initializes a new instance of the <see cref="ProfileMapViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="services">Gives the world's services, or <see langword="null"/> while the host does not run.</param>
    /// <param name="editZone">Opens a zone in the world's Zone tool.</param>
    public ProfileMapViewModel(Localizer localizer, Func<RouteServices?> services, Action<string> editZone)
    {
        _localizer = localizer;
        _services = services;
        _editZone = editZone;
    }

    /// <summary>Shows the settings of an editor, until another is given.</summary>
    /// <param name="editor">The editor, or <see langword="null"/> when the profile shows none.</param>
    public void Use(SettingsEditor? editor)
    {
        if (_editor is { } old)
            old.Edited -= OnEdited;

        _editor = editor;

        if (editor is not null)
            editor.Edited += OnEdited;

        _fit = true;
        Compose();
    }

    /// <summary>Reads the host again: the map appears once it runs, and the project's zones as they change.</summary>
    public void Refresh()
    {
        Compose();
    }

    /// <summary>Says the map's words again, in the app's language.</summary>
    public void Relabel()
    {
        Compose();
    }

    /// <summary>Picks the place the map was clicked on, as the place chosen asks: a zone, or a sub-area.</summary>
    /// <param name="hit">What lay under the pointer.</param>
    public void Clicked(WorldMapHit hit)
    {
        if (_editor is null || _services() is not { } services)
            return;

        switch (PlaceOf())
        {
            case Zone when hit.Kind is WorldMapHitKind.Zone && hit.Id is { } id && _editor.FieldOf(Zone) is ZoneFieldViewModel field:
                field.Choose(id);

                break;
            case SubAreas when hit.Kind is not WorldMapHitKind.None && _editor.FieldOf("SubAreas") is SubAreasFieldViewModel field && SubAreaAt(services, hit.X, hit.Y) is { } subArea:
                field.ToggleSubArea(subArea);

                break;
        }
    }

    /// <summary>Names what lies under the pointer.</summary>
    /// <param name="hit">What lies there.</param>
    public void Hover(WorldMapHit hit)
    {
        Hovered = NameAt(hit);
    }

    [RelayCommand]
    private void EditZone()
    {
        if (CanEditZone && PickedZone is { } id)
            _editZone(id);
    }

    private void OnEdited(object? sender, EventArgs e)
    {
        Compose();
    }

    private string? PlaceOf()
    {
        return _editor?.FieldOf("Place") is { } place && _editor.Draft is { } draft ? place.Definition.GetValue(draft)?.ToString() : null;
    }

    private string NameAt(WorldMapHit hit)
    {
        if (_editor is null || _services() is not { } services || hit.Kind is WorldMapHitKind.None)
            return "";

        if (hit.Kind is WorldMapHitKind.Zone && hit.Id is { } id)
            return services.Library.Find<ZoneAsset>(id)?.Name ?? "";

        return PlaceOf() is SubAreas && _editor.FieldOf("SubAreas") is { } field && SubAreaAt(services, hit.X, hit.Y) is { } subArea ? _editor.OptionOf(field.Definition, subArea).Name : "";
    }

    private int? SubAreaAt(RouteServices services, int x, int y)
    {
        // An inside shares the coordinates of a map of the world: the sub-area is that of the map the world's image shows.
        foreach (var map in services.Index.MapsAt(Layer, x, y))
        {
            if (services.Atlas.TryGetPosition(map, out var position) && position.WorldMap >= 0)
                return position.SubAreaId;
        }

        return null;
    }

    private void Compose()
    {
        if (_editor is null || _services() is not { } services)
        {
            Source = null;
            IsAvailable = false;
            Scene = WorldMapScene.Empty;
            Hint = "";
            CanEditZone = false;
            PickedZone = null;

            return;
        }

        if (Source is null)
        {
            Source = new WorldMapSource(services.Catalog, services.Index, services.ReadTile);
            Layer = services.Catalog.TryGet(1, out _) ? 1 : services.Catalog.Worlds.Span[0].Id;
        }

        IsAvailable = true;

        var place = PlaceOf();
        var zones = services.Library.OfType<ZoneAsset>();
        var picked = place is Zone && _editor.FieldOf(Zone) is ZoneFieldViewModel { Value: { } id } ? services.Library.Find<ZoneAsset>(id) : null;

        PickedZone = picked?.Id;
        CanEditZone = picked is not null;

        var chosen = place switch
        {
            Zone when picked is not null => MapsOfZone(services, picked),
            SubAreas => MapsOfSubAreas(services),
            _ => [],
        };

        // The world follows what is picked: the first of its maps decides, and the map stays where it is for a pick on the same world.
        if (chosen.Count > 0 && !chosen.Any(map => map.Layer == Layer))
            Layer = chosen[0].Layer;

        var squares = chosen.Where(map => map.Layer == Layer).Select(map => (map.X, map.Y)).Distinct().Select(map => new WorldMapZoneSquare(map.X, map.Y, false)).ToList();
        var backdrop = new List<WorldMapBackdropZone>();

        foreach (var zone in zones)
        {
            var held = PlacesOf(services, ZoneMaps.Resolve(zone, services.Atlas)).Where(map => map.Layer == Layer).Select(map => (map.X, map.Y)).Distinct().ToList();

            if (held.Count > 0)
                backdrop.Add(new WorldMapBackdropZone(zone.Id, zone.Name, held, zone.Id == picked?.Id));
        }

        Scene = new WorldMapScene([], [], squares, new WorldMapBackdrop([], backdrop, []));
        Hint = place switch
        {
            Zone => _localizer[zones.Count > 0 ? "ProfileMapHintZone" : "ProfileMapNoZone"],
            SubAreas => _localizer["ProfileMapHintSubAreas"],
            Automatic => _localizer["ProfileMapHintAutomatic"],
            _ => "",
        };

        if (_fit)
        {
            _fit = false;
            FitRequested?.Invoke();
        }
    }

    private static List<(int Layer, int X, int Y)> MapsOfZone(RouteServices services, ZoneAsset zone)
    {
        return PlacesOf(services, ZoneMaps.Resolve(zone, services.Atlas));
    }

    private List<(int Layer, int X, int Y)> MapsOfSubAreas(RouteServices services)
    {
        if (_editor?.FieldOf("SubAreas") is not { } field || _editor.Draft is not { } draft)
            return [];

        var chosen = field.Definition.GetValues(draft).Cast<int>().ToHashSet();

        if (chosen.Count == 0)
            return [];

        var maps = new List<long>();

        foreach (var position in services.Atlas.Positions.Span)
        {
            if (position.WorldMap >= 0 && chosen.Contains(position.SubAreaId))
                maps.Add(position.MapId);
        }

        return PlacesOf(services, maps);
    }

    private static List<(int Layer, int X, int Y)> PlacesOf(RouteServices services, IEnumerable<long> maps)
    {
        var places = new List<(int Layer, int X, int Y)>();

        foreach (var map in maps)
        {
            if (services.Index.TryPlace(map, out var layer, out var x, out var y))
                places.Add((layer, x, y));
        }

        return places;
    }
}
