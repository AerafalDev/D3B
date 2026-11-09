using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls.WorldMaps;
using D3B.App.Localization;
using D3B.App.ViewModels.Assets;
using D3B.App.ViewModels.Characters;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Zones;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.World;

/// <summary>The Outliner: what the world holds, characters, routes with their steps as children, zones, each with an eye; a click selects, and the viewport selects here in turn (D395, D397).</summary>
/// <remarks>
/// <para>
/// The rows are read from the characters panel's model, the project and the editors that are open, every 250 ms, and the
/// list is changed only where it differs, so that a scroll or a row being pressed stays. The selection is the world's
/// (<see cref="WorldSelection"/>): this panel shows it and sets it, as the viewport and the Details panel do.
/// </para>
/// <para>An eye closed leaves what it names off the map, not off the project.</para>
/// </remarks>
internal sealed partial class OutlinerViewModel : ObservableObject
{
    private const double StepIndent = 36;
    private const double ItemIndent = 14;

    private static readonly IBrush _green = new ImmutableSolidColorBrush(Color.Parse("#4ADE80"));
    private static readonly IBrush _red = new ImmutableSolidColorBrush(Color.Parse("#EA5D52"));
    private static readonly IBrush _amber = new ImmutableSolidColorBrush(Color.Parse("#E9AD3C"));
    private static readonly IBrush _grey = new ImmutableSolidColorBrush(Color.Parse("#6B7280"));

    private readonly Localizer _localizer;
    private readonly WorldSelection _selection;
    private readonly WorldViewportViewModel _world;
    private readonly CharactersViewModel _characters;
    private readonly RoutesViewModel _routes;
    private readonly ZonesViewModel _zones;
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private string? _signature;

    /// <summary>Gets the characters' model, whose menu a character's row opens.</summary>
    public CharactersViewModel Characters => _characters;

    /// <summary>Gets the rows, in the order the panel draws them.</summary>
    public ObservableCollection<OutlinerRowViewModel> Rows { get; } = [];

    /// <summary>Gets or sets the text the rows are filtered by.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    /// <summary>Gets whether nothing is in the world: no character, no route, no zone, or nothing matches the filter.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets or sets what shows the world's tab once something is selected in the Outliner.</summary>
    public Action? Show { get; set; }

    /// <summary>Initializes a new instance of the <see cref="OutlinerViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="selection">What is selected in the world.</param>
    /// <param name="world">The viewport, whose eyes the rows open and close.</param>
    /// <param name="characters">The characters, online and not.</param>
    /// <param name="routes">The routes.</param>
    /// <param name="zones">The zones.</param>
    public OutlinerViewModel(Localizer localizer, WorldSelection selection, WorldViewportViewModel world, CharactersViewModel characters, RoutesViewModel routes, ZonesViewModel zones)
    {
        _localizer = localizer;
        _selection = selection;
        _world = world;
        _characters = characters;
        _routes = routes;
        _zones = zones;
        selection.Changed += () => _signature = null;
    }

    private static string Describe(OutlinerRowViewModel row)
    {
        return string.Join('|', row.Key, row.Title, row.Detail, row.Count, row.IsSelected, row.IsExpanded, row.IsEyeOpen, row.IsDirty, row.IsDimmed, row.StateBrush is ISolidColorBrush state ? state.Color : null, row.Head is not null, row.Dots.Count);
    }

    private static void Copy(OutlinerRowViewModel from, OutlinerRowViewModel to)
    {
        to.Title = from.Title;
        to.Detail = from.Detail;
        to.Count = from.Count;
        to.Head = from.Head;
        to.PortraitColor = from.PortraitColor;
        to.StateBrush = from.StateBrush;
        to.IsDimmed = from.IsDimmed;
        to.IsSelected = from.IsSelected;
        to.IsExpanded = from.IsExpanded;
        to.IsEyeOpen = from.IsEyeOpen;
        to.IsDirty = from.IsDirty;
        to.Dots = from.Dots;
    }

    private static IBrush StateOf(PlayState state)
    {
        return state switch
        {
            PlayState.Playing => _green,
            PlayState.Fighting => _red,
            PlayState.Paused => _amber,
            _ => _grey,
        };
    }

    private static IReadOnlyList<IBrush> DotsOf(IReadOnlyList<WorldMapActionKind> kinds)
    {
        return [.. kinds.Select(kind => (IBrush)new ImmutableSolidColorBrush(WorldMapBackdropLayer.ColorOf(kind)))];
    }

    /// <summary>Reads the world again, and changes the rows where they differ.</summary>
    public void Refresh()
    {
        var rows = Build();
        var signature = string.Join('\n', rows.Select(Describe));

        if (signature == _signature)
            return;

        _signature = signature;

        var kept = new Dictionary<string, OutlinerRowViewModel>(StringComparer.Ordinal);

        foreach (var row in Rows)
            kept[row.Key] = row;

        var next = new List<OutlinerRowViewModel>(rows.Count);

        foreach (var row in rows)
        {
            if (kept.TryGetValue(row.Key, out var old) && old.Kind == row.Kind)
            {
                Copy(row, old);
                next.Add(old);
            }
            else
            {
                next.Add(row);
            }
        }

        for (var i = 0; i < next.Count; i++)
        {
            if (i < Rows.Count && ReferenceEquals(Rows[i], next[i]))
                continue;

            var at = Rows.IndexOf(next[i]);

            if (at >= 0)
                Rows.Move(at, i);
            else
                Rows.Insert(i, next[i]);
        }

        while (Rows.Count > next.Count)
            Rows.RemoveAt(Rows.Count - 1);

        IsEmpty = Rows.All(row => row.IsHeading);
    }

    /// <summary>Selects what a row names: a character, a route, one of its steps, a zone.</summary>
    /// <param name="row">The row.</param>
    [RelayCommand]
    public void Select(OutlinerRowViewModel? row)
    {
        if (row is null)
            return;

        switch (row.Kind)
        {
            case OutlinerRowKind.Character when CharacterKeys.Parse(row.Id) is { } key:
                _characters.Choose(key);

                break;
            case OutlinerRowKind.Route:
                _expanded.Add(row.Id);
                _selection.SelectRoute(row.Id);

                break;
            case OutlinerRowKind.Step:
                _expanded.Add(row.Id);
                _selection.SelectRoute(row.Id, row.Step);

                break;
            case OutlinerRowKind.Zone:
                _selection.SelectZone(row.Id);

                break;
            default:
                return;
        }

        Show?.Invoke();
        _signature = null;
        Refresh();
    }

    /// <summary>Opens a character's tab, or shows a route or a zone whole on the map: a double click.</summary>
    /// <param name="row">The row.</param>
    [RelayCommand]
    public void Open(OutlinerRowViewModel? row)
    {
        if (row is { Kind: OutlinerRowKind.Character } && CharacterKeys.Parse(row.Id) is { } key)
            _characters.Open(key);
        else
            Select(row);
    }

    /// <summary>Opens or closes the eye of a row: the map shows what it names or leaves it out.</summary>
    /// <param name="row">The row.</param>
    [RelayCommand]
    public void ToggleEye(OutlinerRowViewModel? row)
    {
        if (row is not { HasEye: true })
            return;

        _world.ToggleVisible(row.Key);
        row.IsEyeOpen = !_world.IsHidden(row.Key);
    }

    /// <summary>Unfolds a route into its steps, or folds it.</summary>
    /// <param name="row">The row.</param>
    [RelayCommand]
    public void ToggleExpanded(OutlinerRowViewModel? row)
    {
        if (row is not { CanExpand: true })
            return;

        if (!_expanded.Remove(row.Id))
            _expanded.Add(row.Id);

        _signature = null;
        Refresh();
    }

    partial void OnFilterChanged(string value)
    {
        _signature = null;
        Refresh();
    }

    private bool Matches(params string[] texts)
    {
        var filter = Filter.Trim();

        return filter.Length == 0 || texts.Any(text => text.Contains(filter, StringComparison.CurrentCultureIgnoreCase));
    }

    private List<OutlinerRowViewModel> Build()
    {
        var rows = new List<OutlinerRowViewModel>();

        AddCharacters(rows);
        AddRoutes(rows);
        AddZones(rows);

        return rows;
    }

    private void AddCharacters(List<OutlinerRowViewModel> rows)
    {
        var group = new List<OutlinerRowViewModel>();

        foreach (var bot in _characters.Online)
        {
            if (!Matches(bot.Name))
                continue;

            var key = CharacterKeys.Text(bot.Bot.Key);
            // The step of its run when it has one, as the mockup says "step 4/8", else what its card said: what it does, or that it is stopped.
            var doing = bot.Step.Length > 0 ? bot.Step : bot.CardActivity;

            group.Add(new OutlinerRowViewModel("character:" + key, OutlinerRowKind.Character)
            {
                Id = key,
                Indent = ItemIndent,
                Title = bot.Name,
                Detail = doing,
                Initials = bot.Initials,
                Head = bot.Head,
                PortraitColor = bot.PortraitColor,
                StateBrush = StateOf(bot.PlayState),
                HasEye = true,
                IsEyeOpen = !_world.IsHidden("character:" + key),
                IsSelected = _selection.IsCharacter(bot.Bot.Key),
            });
        }

        foreach (var line in _characters.Offline)
        {
            if (!Matches(line.Name))
                continue;

            var key = CharacterKeys.Text(line.Key);

            group.Add(new OutlinerRowViewModel("character:" + key, OutlinerRowKind.Character)
            {
                Id = key,
                Indent = ItemIndent,
                Title = line.Name,
                Detail = "",
                Initials = line.Initials,
                Head = line.Head,
                PortraitColor = line.PortraitColor,
                IsDimmed = true,
                StateBrush = null,
                HasEye = false,
                IsSelected = _selection.IsCharacter(line.Key),
            });
        }

        if (group.Count == 0)
            return;

        rows.Add(new OutlinerRowViewModel("heading:characters", OutlinerRowKind.Heading)
        {
            Id = "characters",
            Title = _localizer["OutlinerCharacters"],
            Count = _characters.Online.Count > 0 ? _localizer.Format("CharactersOnline", _characters.Online.Count) : "",
        });
        rows.AddRange(group);
    }

    private void AddRoutes(List<OutlinerRowViewModel> rows)
    {
        if (_routes.Services is not { } services)
            return;

        var group = new List<OutlinerRowViewModel>();

        foreach (var route in services.Library.Assets.OfType<RouteAsset>().OrderBy(route => route.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var editor = _routes.Find(route.Id);
            var anchors = editor?.CurrentAnchors ?? route.Anchors;
            var matches = Matches(route.Name);
            var steps = anchors.Select((anchor, index) => (anchor, index, place: PlaceOf(services, anchor.MapId), zone: ZoneOf(services, anchor.MapId))).ToList();
            var stepMatches = steps.Where(step => Matches(step.place, step.zone)).ToList();

            if (!matches && stepMatches.Count == 0)
                continue;

            var key = "route:" + route.Id;
            var expanded = _expanded.Contains(route.Id) || (Filter.Trim().Length > 0 && stepMatches.Count > 0);

            group.Add(new OutlinerRowViewModel(key, OutlinerRowKind.Route)
            {
                Id = route.Id,
                Indent = ItemIndent,
                Title = route.Name,
                Detail = _localizer.Plural("OutlinerSteps", anchors.Count),
                IconKey = AssetKinds.IconOf(AssetKind.Route),
                CanExpand = anchors.Count > 0,
                IsExpanded = expanded && anchors.Count > 0,
                HasEye = true,
                IsEyeOpen = !_world.IsHidden(key),
                IsDirty = editor?.IsDirty == true,
                IsSelected = _selection.IsRoute(route.Id) && _selection.Step < 0,
            });

            if (!expanded)
                continue;

            foreach (var (anchor, index, place, zone) in matches ? steps : stepMatches)
            {
                group.Add(new OutlinerRowViewModel($"step:{route.Id}:{index}", OutlinerRowKind.Step)
                {
                    Id = route.Id,
                    Step = index,
                    Indent = StepIndent,
                    Title = place,
                    Detail = zone,
                    Dots = DotsOf(RouteActionKinds.Of(anchor.Actions)),
                    IsSelected = _selection.IsRoute(route.Id) && _selection.Step == index,
                });
            }
        }

        if (group.Count == 0)
            return;

        rows.Add(new OutlinerRowViewModel("heading:routes", OutlinerRowKind.Heading)
        {
            Id = "routes",
            Title = _localizer["OutlinerRoutes"],
            Count = group.Count(row => row.Kind is OutlinerRowKind.Route).ToString(CultureInfo.CurrentCulture),
        });
        rows.AddRange(group);
    }

    private void AddZones(List<OutlinerRowViewModel> rows)
    {
        if (_routes.Services is not { } services)
            return;

        var group = new List<OutlinerRowViewModel>();

        foreach (var zone in services.Library.Assets.OfType<ZoneAsset>().OrderBy(zone => zone.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!Matches(zone.Name))
                continue;

            var editor = _zones.Find(zone.Id);
            var key = "zone:" + zone.Id;
            var maps = ZoneMaps.Resolve(editor is { IsDirty: true } ? editor.Current : zone, services.Atlas).Count;

            group.Add(new OutlinerRowViewModel(key, OutlinerRowKind.Zone)
            {
                Id = zone.Id,
                Indent = ItemIndent,
                Title = zone.Name,
                Detail = _localizer.Plural("RouteWorldMaps", maps),
                IconKey = AssetKinds.IconOf(AssetKind.Zone),
                HasEye = true,
                IsEyeOpen = !_world.IsHidden(key),
                IsDirty = editor?.IsDirty == true,
                IsSelected = _selection.IsZone(zone.Id),
            });
        }

        if (group.Count == 0)
            return;

        rows.Add(new OutlinerRowViewModel("heading:zones", OutlinerRowKind.Heading)
        {
            Id = "zones",
            Title = _localizer["OutlinerZones"],
            Count = group.Count.ToString(CultureInfo.CurrentCulture),
        });
        rows.AddRange(group);
    }

    private string PlaceOf(RouteServices services, long map)
    {
        if (!services.Atlas.TryGetPosition(map, out var position))
            return _localizer.Format("MapNumber", map);

        var coordinates = _localizer.Format("Coordinates", position.X, position.Y);

        return position.WorldMap < 0 ? _localizer.Format("MapInside", coordinates) : coordinates;
    }

    private string ZoneOf(RouteServices services, long map)
    {
        if (!services.Atlas.TryGetPosition(map, out var position))
            return "";

        return services.Texts is { } texts && services.Atlas.TryGetSubArea(position.SubAreaId, out var subArea) && texts.TryGet(_localizer.GameLanguage, subArea.NameId, out var name)
            ? name
            : _localizer.Format("SubArea", position.SubAreaId);
    }
}
