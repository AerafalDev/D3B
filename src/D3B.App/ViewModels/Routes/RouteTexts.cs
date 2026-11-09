using D3B.Data.WorldMaps;
using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>Says in words where a map is and what a step of a route is, for the route editor's lists, its map and its line under the pointer (D299, D371).</summary>
internal sealed class RouteTexts
{
    private readonly RoutesViewModel _page;

    /// <summary>Initializes a new instance of the <see cref="RouteTexts"/> class.</summary>
    /// <param name="page">The routes, which hold the localizer and what the editors read.</param>
    public RouteTexts(RoutesViewModel page)
    {
        _page = page;
    }

    /// <summary>Gets the key of the text that names a kind of action in the menu.</summary>
    /// <param name="node">The action's node.</param>
    /// <returns>The key.</returns>
    public static string ActionNameKey(string node)
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

    /// <summary>Gives a world its name in the game's language, or its number.</summary>
    /// <param name="world">The world.</param>
    /// <returns>The name.</returns>
    public string WorldName(WorldMap world)
    {
        var services = _page.Services!;

        return services.Texts is { } texts && texts.TryGet(_page.Localizer.GameLanguage, world.NameId, out var name)
            ? name
            : _page.Localizer.Format("RouteWorldNumber", world.Id);
    }

    /// <summary>Tells whether a map lies inside, in a house or a dungeon, where it has no place on the world's map.</summary>
    /// <param name="map">The map's id.</param>
    /// <returns>Whether it is inside.</returns>
    public bool IsInside(long map)
    {
        return _page.Services!.Atlas.TryGetPosition(map, out var position) && position.WorldMap < 0;
    }

    /// <summary>Says where a map is: its coordinates, said inside when it is, or its number.</summary>
    /// <param name="map">The map's id.</param>
    /// <returns>The place.</returns>
    public string Place(long map)
    {
        var localizer = _page.Localizer;

        if (!_page.Services!.Atlas.TryGetPosition(map, out var position))
            return localizer.Format("MapNumber", map);

        var coordinates = localizer.Format("Coordinates", position.X, position.Y);

        return position.WorldMap < 0 ? localizer.Format("MapInside", coordinates) : coordinates;
    }

    /// <summary>Says which sub-area a map is in.</summary>
    /// <param name="map">The map's id.</param>
    /// <returns>The sub-area's name, or its number.</returns>
    public string Zone(long map)
    {
        var services = _page.Services!;
        var localizer = _page.Localizer;

        if (!services.Atlas.TryGetPosition(map, out var position))
            return localizer.Format("MapNumber", map);

        return services.Texts is { } texts && services.Atlas.TryGetSubArea(position.SubAreaId, out var subArea) && texts.TryGet(localizer.GameLanguage, subArea.NameId, out var name)
            ? name
            : localizer.Format("SubArea", position.SubAreaId);
    }

    /// <summary>Says what lies at coordinates of a world: the coordinates alone, a map, or how many maps share them.</summary>
    /// <param name="layer">The world's id.</param>
    /// <param name="x">The abscissa.</param>
    /// <param name="y">The ordinate.</param>
    /// <returns>The text.</returns>
    public string Describe(int layer, int x, int y)
    {
        var maps = _page.Services!.Index.MapsAt(layer, x, y);
        var localizer = _page.Localizer;
        var coordinates = localizer.Format("Coordinates", x, y);

        if (maps.IsEmpty)
            return coordinates;

        var zone = Zone(maps[0]);

        return maps.Length == 1
            ? localizer.Format("RouteHudMap", coordinates, maps[0].ToString("N0", localizer.Culture), zone)
            : localizer.Format("RouteHudMaps", coordinates, maps.Length, zone);
    }

    /// <summary>Says what a step is: its number, its place and how many actions it plays.</summary>
    /// <param name="anchors">The route's anchors.</param>
    /// <param name="index">The step's place.</param>
    /// <returns>The text.</returns>
    public string StepHud(IReadOnlyList<RouteAnchor> anchors, int index)
    {
        var localizer = _page.Localizer;
        var place = Place(anchors[index].MapId);
        var count = anchors[index].Actions.Length;

        return count == 0 ? localizer.Format("RouteHudStep", index + 1, place) : localizer.Format("RouteHudStepActions", index + 1, place, localizer.Plural("RouteActionsCount", count));
    }
}
