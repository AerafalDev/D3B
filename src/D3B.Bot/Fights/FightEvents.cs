using System.Collections.Immutable;
using D3B.Data.Maps;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Fights;

/// <summary>Turns what the server sends during a fight into the fight's next state: pure functions, as the character's (D9).</summary>
/// <remarks>
/// <para>
/// The characteristics, measured (client.md): a fighter's AP and MP left are characteristics 1 and 23, which the server
/// gives again after each cast and step, the character's and each fighter's as its turn begins. The life is 0 plus 11, the
/// vitality, plus 97, the life lacking, as out of fights; a monster's and a summon's carried 0 alone. The server announces
/// each loss and heal apart, and gives the life no more. The AP and MP an action spends are announced too, before or after the characteristics: the state takes
/// the characteristics alone, so that an action's spending is never counted twice.
/// </para>
/// <para>
/// Moves, measured: a fighter walks as out of fights, with no confirmation from its client, teleports onto a cell, or swaps
/// with a target; during the placement, the server gives every fighter's disposition after each move.
/// </para>
/// </remarks>
public static class FightEvents
{
    private const int MostLife = 0;
    private const int ActionPoints = 1;
    private const int Vitality = 11;
    private const int MovementPoints = 23;
    private const int LifeLacking = 97;

    /// <summary>Gives the fight's state once the server sent a message, when the message is the fight's.</summary>
    /// <param name="fight">The fight's state before the message.</param>
    /// <param name="self">The character's id.</param>
    /// <param name="message">The message, readable.</param>
    /// <param name="next">The fight's state after it, or the same state when the message is not the fight's.</param>
    /// <returns>Whether the message is the fight's: a message of the roleplay, such as the map's or the inventory's, is not.</returns>
    public static bool TryApply(FightState fight, long self, IMessage message, out FightState next)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(message);

        FightState? applied = message switch
        {
            FightPlacementPossiblePositionsEvent { Cells: { } cells } positions => fight with
            {
                Team = TeamOf(fight, self, positions),
                ChallengerCells = [.. cells.ChallengerCells.Where(IsCell)],
                DefenderCells = [.. cells.DefenderCells.Where(IsCell)],
            },
            FightPlacementPossiblePositionsEvent positions => fight with { Team = TeamOf(fight, self, positions) },
            EntitiesDispositionEvent dispositions => dispositions.Dispositions
                .Where(disposition => disposition is { HasEntityId: true, HasCellId: true } && IsCell(disposition.CellId))
                .Aggregate(fight, (state, disposition) => Moved(state, disposition.EntityId, disposition.CellId)),
            FightHumanReadyStateEvent ready when ready.CharacterId == self => fight with { IsReady = ready.Ready },
            FightHumanReadyStateEvent => fight,
            FightFighterShowEvent { Fighter: { } shown } => Shown(fight, self, shown),
            FightFighterShowEvent => fight,
            FightSynchronizeEvent synchronized => Synchronized(fight, self, synchronized),
            FightStartEvent => fight with { Phase = FightPhase.Fighting, IsReady = false },
            FightTurnListEvent order => Ordered(fight, order),
            FightNewRoundEvent round => fight with { Round = round.Round },
            FightTurnEvent turn => fight with { PlayingId = turn.FighterId, Round = turn.Round > 0 ? turn.Round : fight.Round, IsOwnTurn = false },
            FightTurnStartPlayingEvent => fight with { IsOwnTurn = fight.PlayingId == self },
            FightTurnEndEvent ended when ended.FighterId == fight.PlayingId => fight with { PlayingId = null, IsOwnTurn = false },
            FightTurnEndEvent => fight,
            FightRefreshCharacterStatsEvent { Stats: { } stats } refreshed => Changed(fight, refreshed.FighterId, fighter => Spent(fighter, stats)),
            FightRefreshCharacterStatsEvent => fight,
            MapMovementEvent walked when walked.Cells.Count > 0 && IsCell(walked.Cells[^1]) => Moved(fight, walked.ActorId, walked.Cells[^1]),
            MapMovementEvent => fight,
            GameActionFightEvent action => Acted(fight, action),
            FightEndEvent end => fight with { Phase = FightPhase.Ended, PlayingId = null, IsOwnTurn = false, End = Ended(end, self) },
            FightStartingEvent or FightJoinEvent or SequenceStartEvent or SequenceEndEvent => fight,
            _ => null,
        };

        next = applied ?? fight;

        return applied is not null;
    }

    private static FightTeam TeamOf(FightState fight, long self, FightPlacementPossiblePositionsEvent positions)
    {
        // The event's team was unset in every fight captured, its meaning not measured: the character's own fighter says it.
        return fight.Fighters.TryGetValue(self, out var own) ? own.Team : positions.Team;
    }

    private static FightState Synchronized(FightState fight, long self, FightSynchronizeEvent synchronized)
    {
        // Every fighter there is: those it leaves out are gone, those it lists keep what the actions told of them.
        var listed = synchronized.Fighters.Select(actor => actor.Id).ToHashSet();
        var kept = fight with { Fighters = fight.Fighters.RemoveRange(fight.Fighters.Keys.Where(id => !listed.Contains(id))) };

        return synchronized.Fighters.Aggregate(kept, (state, actor) => Shown(state, self, actor));
    }

    private static FightState Shown(FightState fight, long self, Actor actor)
    {
        var known = fight.Fighters.GetValueOrDefault(actor.Id) ?? new Fighter { Id = actor.Id };
        var cell = actor.Disposition is { HasCellId: true } at && IsCell(at.CellId) ? at.CellId : known.Cell;
        var shown = actor.Information?.Fighter;

        if (shown is null)
            return fight with { Fighters = fight.Fighters.SetItem(actor.Id, known with { Cell = cell }) };

        var fighter = Lived(known with
        {
            Cell = cell,
            Team = shown.Spawn?.Team ?? known.Team,
            IsAlive = shown.Spawn?.Alive ?? known.IsAlive,
            Level = shown.Named?.Character?.Level ?? shown.Ai?.Monster?.Level ?? known.Level,
            BreedId = shown.Named?.Character?.BreedId ?? known.BreedId,
            MonsterId = shown.Ai?.Monster?.MonsterId ?? known.MonsterId,
            Grade = shown.Ai?.Monster?.Grade ?? known.Grade,
            SummonerId = shown.Stats is { Summoned: true } summoned ? summoned.SummonerId : known.SummonerId,
        }, shown.Stats);

        return fight with
        {
            Fighters = fight.Fighters.SetItem(actor.Id, fighter),
            Team = actor.Id == self && shown.Spawn is { } spawn ? spawn.Team : fight.Team,
        };
    }

    private static FightState Ordered(FightState fight, FightTurnListEvent order)
    {
        var entries = order.Entries.Where(entry => entry.Fighter is not null).Select(entry => entry.Fighter).ToList();
        var dead = entries.Where(entry => entry.Dead).Aggregate(fight, (state, entry) => Changed(state, entry.FighterId, Died));

        return dead with { TurnOrder = [.. entries.Select(entry => entry.FighterId)] };
    }

    private static FightState Acted(FightState fight, GameActionFightEvent action)
    {
        return action.ActionCase switch
        {
            GameActionFightEvent.ActionOneofCase.Death => Changed(fight, action.Death.TargetId, Died),
            GameActionFightEvent.ActionOneofCase.Teleport when IsCell(action.Teleport.CellId) => Moved(fight, action.Teleport.TargetId, action.Teleport.CellId),
            GameActionFightEvent.ActionOneofCase.Swap => Swapped(fight, action.SourceId, action.Swap.TargetId),
            GameActionFightEvent.ActionOneofCase.LifeLost => Changed(fight, action.LifeLost.TargetId, fighter => fighter with
            {
                Life = Math.Max(0, fighter.Life - action.LifeLost.Loss),
                MaxLife = Math.Max(0, fighter.MaxLife - action.LifeLost.PermanentLoss),
            }),
            GameActionFightEvent.ActionOneofCase.LifeGained => Changed(fight, action.LifeGained.TargetId, fighter => fighter with
            {
                Life = Math.Min(fighter.MaxLife, fighter.Life + action.LifeGained.Amount),
            }),
            GameActionFightEvent.ActionOneofCase.Summons when action.Summons.Fighters is { } summoned => summoned.Summons.Aggregate(fight, Summoned),
            _ => fight,
        };
    }

    private static FightState Summoned(FightState fight, SummonedFighter summon)
    {
        // Measured: one position per summon, which names its id and cell; it has no actor of its own.
        if (summon.Positions.FirstOrDefault() is not { Position: { } position } spawn)
            return fight;

        var cell = position.Disposition is { HasCellId: true } at && IsCell(at.CellId) ? at.CellId : (int?)null;
        var monster = summon.Entity?.Monster;
        var fighter = Lived(new Fighter
        {
            Id = position.Id,
            Team = spawn.Team,
            Cell = cell,
            IsAlive = spawn.Alive,
            MonsterId = monster?.MonsterId,
            Grade = monster is { GradeOrLevelCase: SummonedMonster.GradeOrLevelOneofCase.Grade } ? monster.Grade : null,
            SummonerId = summon.Stats is { Summoned: true } stats ? stats.SummonerId : null,
        }, summon.Stats);

        return fight with { Fighters = fight.Fighters.SetItem(fighter.Id, fighter) };
    }

    private static FightState Swapped(FightState fight, long sourceId, long targetId)
    {
        if (!fight.Fighters.TryGetValue(sourceId, out var source) || !fight.Fighters.TryGetValue(targetId, out var target))
            return fight;

        return fight with { Fighters = fight.Fighters.SetItem(sourceId, source with { Cell = target.Cell }).SetItem(targetId, target with { Cell = source.Cell }) };
    }

    private static FightState Moved(FightState fight, long fighterId, int cell)
    {
        return Changed(fight, fighterId, fighter => fighter with { Cell = cell });
    }

    private static FightState Changed(FightState fight, long fighterId, Func<Fighter, Fighter> change)
    {
        return fight.Fighters.TryGetValue(fighterId, out var fighter) ? fight with { Fighters = fight.Fighters.SetItem(fighterId, change(fighter)) } : fight;
    }

    private static Fighter Died(Fighter fighter)
    {
        return fighter with { IsAlive = false, Life = 0, Cell = null };
    }

    private static Fighter Lived(Fighter fighter, FightCharacteristics? stats)
    {
        if (stats is null)
            return fighter;

        var spent = Spent(fighter, stats);

        if (ValueOf(stats, MostLife) is not { } most)
            return spent;

        var max = (int)(most + (ValueOf(stats, Vitality) ?? 0));

        return spent with { MaxLife = max, Life = (int)Math.Max(0, max + (ValueOf(stats, LifeLacking) ?? 0)) };
    }

    private static Fighter Spent(Fighter fighter, FightCharacteristics stats)
    {
        return fighter with
        {
            ActionPoints = (int)(ValueOf(stats, ActionPoints) ?? fighter.ActionPoints),
            MovementPoints = (int)(ValueOf(stats, MovementPoints) ?? fighter.MovementPoints),
        };
    }

    private static long? ValueOf(FightCharacteristics stats, int id)
    {
        var found = stats.Characteristics.FirstOrDefault(characteristic => characteristic.Id == id);

        return found?.KindCase switch
        {
            null => null,
            Characteristic.KindOneofCase.Usable => (long)found.Usable.Number1 + found.Usable.Number2 + found.Usable.Number3 + found.Usable.Number4 + found.Usable.Number5 + found.Usable.Number6,
            Characteristic.KindOneofCase.Value => found.Value.Total,
            Characteristic.KindOneofCase.Detailed => (long)found.Detailed.Number1 + found.Detailed.Number2 + found.Detailed.Number3 + found.Detailed.Number4 + found.Detailed.Number5 + found.Detailed.Number7,
            _ => 0,
        };
    }

    private static bool IsCell(int cell)
    {
        return cell is >= 0 and < MapData.CellCount;
    }

    private static FightEnd? Ended(FightEndEvent end, long self)
    {
        var result = end.Results.FirstOrDefault(result => result.Fighter?.FighterId == self);

        if (result is null)
            return null;

        var experience = result.Fighter.Character?.Data.Select(data => data.Experience).FirstOrDefault(found => found is not null)?.Gained ?? 0;
        var items = (result.Loot?.Objects.AsEnumerable() ?? []).SelectMany(group => group.Objects)
            .GroupBy(item => item.ItemId)
            .ToImmutableSortedDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        return new FightEnd(result.Outcome, experience, result.Loot?.Kamas ?? 0, items);
    }
}
