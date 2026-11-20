using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Scripts.Fights;

/// <summary>Replays every placement of the characters in the captures through the fight's AI, the turn order left out and weighed (D478).</summary>
/// <remarks>
/// As the fight starts, the character's place in the turn order is read: first of the fight, before some enemies, or after
/// every enemy. Its cell as placed, the cell the AI chooses with every enemy's threat and the one it chooses with only the
/// threat of the enemies playing before it are each judged by the same numbers: the life those enemies may take there, and
/// the damage it may deal there in its first turn. Then the life it lost before its first turn, as the server announced it.
/// It prints counts and sums by place in the order, never a value of one fight.
/// </remarks>
internal sealed class PlacementBench
{
    private static readonly string[] _places = ["first", "before some enemies", "after every enemy"];
    private static readonly string[] _cells = ["as placed", "every enemy's threat", "by the turn order"];

    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly FightTactics _tactics;
    private readonly TacticWeights _weights;
    private readonly Dictionary<long, FightMap> _fightMaps = [];
    private readonly Dictionary<string, double> _sums = new(StringComparer.Ordinal);

    public PlacementBench(GameTables tables, GameMaps maps, TacticWeights weights)
    {
        _tables = tables;
        _maps = maps;
        _tactics = new FightTactics(tables);
        _weights = weights;
    }

    public void Replay(List<IMessage> messages)
    {
        CharacterState? state = null;
        string? waiting = null;

        foreach (var message in messages)
        {
            if (message is CharacterSelectionEvent { Success.Character: { } chosen })
            {
                state = CharacterState.Chosen(new CharacterKey(0, chosen.Id), chosen);
                waiting = null;
            }

            if (state is null)
                continue;

            var self = state.Key.CharacterId;

            if (message is FightStartEvent && state.Fight is { Phase: FightPhase.Placement } placing)
                waiting = Placed(state, placing);

            // The life the enemies took from the character before its first turn, as the server announced it.
            if (waiting is not null && message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.LifeLost } lost && lost.LifeLost.TargetId == self
                && state.Fight is { } between && between.Fighters.TryGetValue(lost.SourceId, out var source) && between.Fighters.TryGetValue(self, out var own) && source.Team != own.Team)
            {
                Add($"{waiting}: life lost before the first turn", lost.LifeLost.Loss + (lost.LifeLost.HasShieldLoss ? lost.LifeLost.ShieldLoss : 0));
            }

            state = CharacterEvents.Apply(state, RelayDirection.FromServer, message);

            if (message is FightTurnStartPlayingEvent && state.Fight is { IsOwnTurn: true, PlayingId: { } playing } && playing == self || message is FightEndEvent)
                waiting = null;
        }
    }

    public void Write(TextWriter writer)
    {
        writer.WriteLine("placements, by the character's place in the turn order (D478)");
        writer.WriteLine($"  {string.Empty,-40}{"threat",10}{"strike",10}");

        foreach (var company in new[] { "alone", "in a group" })
        {
            foreach (var place in _places)
            {
                var key = $"{company}, {place}";
                var count = _sums.GetValueOrDefault($"{key}: placements");

                if (count is 0)
                    continue;

                writer.WriteLine($"  {key}: {count:F0} placements, the order's cell other than every threat's {_sums.GetValueOrDefault($"{key}: moved"):F0}, life lost before the first turn {_sums.GetValueOrDefault($"{key}: life lost before the first turn"):F0}");

                foreach (var cell in _cells)
                    writer.WriteLine($"    {cell,-38}{_sums.GetValueOrDefault($"{key}, {cell}: threat"),10:F0}{_sums.GetValueOrDefault($"{key}, {cell}: strike"),10:F0}");
            }
        }
    }

    private static string Company(ImaginedFight fight)
    {
        return fight.Fighters.Any(fighter => fighter.IsCharacter && fighter.Team == fight.Actor.Team && fighter.Id != fight.ActorId) ? "in a group" : "alone";
    }

    private string? Placed(CharacterState state, FightState placing)
    {
        if (state.MapId is not { } mapId || !_maps.TryGet(mapId, out var data))
            return null;

        if (!_fightMaps.TryGetValue(mapId, out var map))
            _fightMaps[mapId] = map = FightMap.Of(data);

        if (ImaginedFights.Of(state, map, _tables, _tactics.Blows, null) is not { } imagined || placing.OwnCells.IsDefaultOrEmpty)
            return null;

        var fight = imagined.Fight;
        var before = FightTactics.PlayingBefore(fight, placing.TurnOrder);

        if (before is null)
            return null;

        var enemies = fight.Fighters.Count(fighter => fighter.IsAlive && fighter.Team != fight.Actor.Team);
        var place = before.Count is 0 ? _places[0] : before.Count < enemies ? _places[1] : _places[2];
        var key = $"{Company(fight)}, {place}";
        var judge = _tactics.JudgeOf(fight, imagined.Knowledge, _weights);
        var every = _tactics.Place(fight, placing.OwnCells, imagined.Knowledge, _weights);
        var ordered = _tactics.Place(fight, placing.OwnCells, imagined.Knowledge, _weights, placing.TurnOrder);

        Add($"{key}: placements", 1);
        Add($"{key}: moved", every == ordered ? 0 : 1);

        foreach (var (cell, name) in new[] { (fight.Actor.Cell, _cells[0]), (every, _cells[1]), (ordered, _cells[2]) })
        {
            var there = fight.With(fight.Actor with { Cell = cell });

            Add($"{key}, {name}: threat", judge.Threat(there, fight.ActorId, before));
            Add($"{key}, {name}: strike", judge.NextTurnDamage(there));
        }

        return key;
    }

    private void Add(string key, double value)
    {
        _sums[key] = _sums.GetValueOrDefault(key) + value;
    }
}
