using System.Globalization;
using D3B.App.Localization;
using D3B.App.ViewModels.Maps;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Npcs;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Routes;

/// <summary>Makes the rows of the actions of a step, each with the form of its settings when the host knows its node (D377).</summary>
internal static class RouteActionForms
{
    /// <summary>Makes a row for each action of a step.</summary>
    /// <param name="services">What the editors read: the nodes, the game's references, the maps and the texts.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs, which a form may open.</param>
    /// <param name="anchor">The step.</param>
    /// <param name="edited">What a form does when one of its values is edited: the action's place, its node and the form.</param>
    /// <param name="remove">What removing a row does, given its action's place.</param>
    /// <param name="move">What moving a row does, given its action's place and how many places.</param>
    /// <returns>The rows, in order.</returns>
    public static List<RouteActionViewModel> Build(RouteServices services, Localizer localizer, Dialogs dialogs, RouteAnchor anchor, Action<int, NodeDefinition, SettingsEditor> edited, Action<int> remove, Action<int, int> move)
    {
        var rows = new List<RouteActionViewModel>(anchor.Actions.Length);
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
                var created = new SettingsEditor(made.Values, RouteActionCalls.Read(made, call).Settings, services.Game, names, localizer, services.Provider, [], dialogs) { Map = map, Npcs = npcs, NpcNames = new NpcNames(localizer, services.Provider?.GetService<GameTables>(), services.Texts), Place = places.Place };

                created.Edited += (_, _) => edited(index, made, created);
                editor = created;
            }

            rows.Add(new RouteActionViewModel(i, anchor.Actions.Length, call.Node, definition, editor, localizer, (kind, id) => NameOf(services, names, kind, id), remove, move));
        }

        return rows;
    }

    private static string NameOf(RouteServices services, GameNames names, SettingKind kind, int id)
    {
        var game = services.Game;

        return game is not null && game.Options(kind).FirstOrDefault(option => option.Id == id) is { } reference
            ? names.Name(kind, reference)
            : "#" + id.ToString(CultureInfo.InvariantCulture);
    }
}
