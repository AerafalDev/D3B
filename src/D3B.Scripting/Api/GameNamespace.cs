using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Maps;
using Lua;

namespace D3B.Scripting.Api;

/// <summary>The <c>game</c> namespace: the installed build's data, which the pack holds, read without the bot.</summary>
internal static class GameNamespace
{
    public static ScriptNamespace Create(ScriptBinding binding)
    {
        return new ScriptNamespace("game", binding)
            .Add("mapsAt", MapsAt)
            .Add("position", Position)
            .Add("distance", Distance)
            .Add("cell", Cell)
            .Add("mapActions", MapActions);
    }

    private static int MapsAt(ScriptCall call)
    {
        var x = call.Integer(0, short.MinValue, short.MaxValue);
        var y = call.Integer(1, short.MinValue, short.MaxValue);
        var maps = new LuaTable();
        var index = 1;

        foreach (var position in Atlas(call).Positions.Span)
        {
            if (position.X == x && position.Y == y)
                maps[index++] = position.MapId;
        }

        return call.Return(maps);
    }

    private static int Position(ScriptCall call)
    {
        var mapId = call.Integer(0);

        if (!Atlas(call).TryGetPosition(mapId, out var position))
            return call.Return(LuaValue.Nil);

        return call.Return(new LuaTable { ["x"] = position.X, ["y"] = position.Y, ["subArea"] = position.SubAreaId, ["worldMap"] = position.WorldMap });
    }

    private static int Distance(ScriptCall call)
    {
        var from = call.Integer(0, 0, MapData.CellCount - 1);
        var to = call.Integer(1, 0, MapData.CellCount - 1);

        return call.Return(MapGeometry.Distance(from, to));
    }

    private static int Cell(ScriptCall call)
    {
        var mapId = call.Integer(0);
        var cell = call.Integer(1, 0, MapData.CellCount - 1);
        var maps = call.Binding.Maps ?? throw call.Error("the game's data is read once the bot runs the script");

        if (!maps.TryGet(mapId, out var map))
            return call.Return(LuaValue.Nil);

        var data = map.Cell(cell);

        return call.Return(new LuaTable { ["walkable"] = data.IsWalkable, ["zone"] = data.Zone });
    }

    private static int MapActions(ScriptCall call)
    {
        var mapId = call.Integer(0);
        var graph = (call.Binding.World ?? throw call.Error("the game's data is read once the bot runs the script")).Graph;
        var vertices = graph.Vertices.Span;
        var cells = new SortedSet<int>();

        // A map action moves whoever steps on its cell, from any zone of the map (D73).
        for (var vertex = 0; vertex < vertices.Length; vertex++)
        {
            if (vertices[vertex].MapId != mapId)
                continue;

            foreach (var edge in graph.EdgesFrom(vertex))
            {
                foreach (var transition in graph.TransitionsOf(edge))
                {
                    if (transition.Type is WorldTransitionType.MapAction)
                        cells.Add(transition.CellId);
                }
            }
        }

        var table = new LuaTable();
        var index = 1;

        foreach (var cell in cells)
            table[index++] = cell;

        return call.Return(table);
    }

    private static WorldAtlas Atlas(ScriptCall call)
    {
        // The pack's world reaches the script with its first hook: its text runs within the first step.
        return call.Binding.World?.Atlas ?? throw call.Error("the game's data is read once the bot runs the script");
    }
}
