using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Tactics;

/// <summary>A challenge that asks only of the turns of the fighter playing: its movement points, its casts, where it ends them (D464).</summary>
/// <remarks>
/// <para>
/// The owner's list (D464), read from the client's completion criteria (client.md): Zombie, Statue, Nomade, Versatile,
/// Collant, Misanthrope, Prudent, Raser les murs, De la même lignée, La diagonale du plein, En ligne de mire and Pas vu pas
/// pris, 49% of the 2,976 fights captured. A search keeps it (<see cref="FightTactics.Plan"/>)
/// only while the turn it costs stays under <see cref="MostLoss"/>.
/// </para>
/// <para>
/// The movement points used are those the fighter began its turn with, as the knowledge gives them, less those it holds: a
/// fighter that gained or lost some through a spell is judged on what it holds, and Nomade's partial escape, a walk away from
/// an enemy that takes more than its cells, is not foreseen.
/// </para>
/// </remarks>
public sealed record TurnChallenge
{
    /// <summary>Gets what the challenge asks of the movement points used.</summary>
    public TurnMovement Movement { get; init; }

    /// <summary>Gets whether each spell, the weapon's blow among them, is cast at most once a turn: Versatile.</summary>
    public bool EachActionOnce { get; init; }

    /// <summary>Gets where the turn ends.</summary>
    public TurnEnd End { get; init; }

    /// <summary>Gets the cell the fighter playing began its turn on, which Statue's turn ends on; null when not known.</summary>
    /// <remarks>A jump, a teleport or a push of its own spell moves the fighter with its movement points whole.</remarks>
    public int? StartCell { get; init; }

    /// <summary>Gets the most of the turn's gain the challenge may cost, from 0 to 1, or more once the turn played for it.</summary>
    /// <remarks>
    /// The owner's choice (D464): half of what keeps the reward a minute the same. A challenge won adds its bonus b to the
    /// fight's reward, so a fight slower by a share l of its pace still earns as much a minute while l ≤ b / (1 + b); half of it,
    /// since a turn's challenge is lost at the first turn that breaks it, so that a costly turn may be paid for nothing.
    /// </remarks>
    public double MostLoss { get; init; }

    /// <summary>Gets the challenge a completion criterion asks, when it asks only of the turns.</summary>
    /// <param name="completion">The criterion, as the client writes it.</param>
    /// <param name="bonus">What winning it adds to the reward, in percent, as the server said.</param>
    /// <returns>The challenge; null when the criterion is not one the AI keeps.</returns>
    public static TurnChallenge? Of(string? completion, int bonus)
    {
        var challenge = completion switch
        {
            "TM=1,0" => new TurnChallenge { Movement = TurnMovement.OnePoint },
            "Tm<1,0" => new TurnChallenge { Movement = TurnMovement.Still },
            "TM=m,0&TT=0,0" => new TurnChallenge { Movement = TurnMovement.AllPoints },
            "SAt0" => new TurnChallenge { EachActionOnce = true },
            "TD<2,hc0,hc0" => new TurnChallenge { End = TurnEnd.BesideAnAlly },
            "TD>1,hc0,hc0" => new TurnChallenge { End = TurnEnd.AwayFromAllies },
            "TD>1,hc0,e1" => new TurnChallenge { End = TurnEnd.AwayFromEnemies },
            "TD<2,hc0,whb" => new TurnChallenge { End = TurnEnd.BesideAnObstacle },
            "SP=te,l,hc0,hc0" => new TurnChallenge { End = TurnEnd.InLineWithAnAlly },
            "SP=te,d,hc0,hc0" => new TurnChallenge { End = TurnEnd.InDiagonalWithAnAlly },
            "SV=te,hc0,e1,s1,a" => new TurnChallenge { End = TurnEnd.InSightOfAnEnemy },
            "SV=te,hc0,e1,s0,v" => new TurnChallenge { End = TurnEnd.OutOfEnemiesSight },
            _ => null,
        };

        var share = Math.Max(0, bonus) / 100.0;

        return challenge is null ? null : challenge with { MostLoss = 0.5 * share / (1 + share) };
    }

    private static int Used(ImaginedFight fight, FightKnowledge knowledge, int movementPointsLeft)
    {
        var start = knowledge.TurnPoints.TryGetValue(fight.ActorId, out var points) ? points.MovementPoints : fight.Actor.MovementPoints;

        return start - movementPointsLeft;
    }

    private static IEnumerable<int> Others(ImaginedFight fight, Combatant actor, bool ally)
    {
        // Every living fighter of the side, its summons among them: the challenges speak of the allied fighters.
        foreach (var fighter in fight.Fighters)
        {
            if (fighter.Id != actor.Id && fighter.IsAlive && fighter.Cell is not Combatant.NoCell && (fighter.Team == actor.Team) == ally)
                yield return fighter.Cell;
        }
    }

    private static bool BesideAnObstacle(FightMap map, int cell)
    {
        // The client's description (D464): holes and the map's edges are obstacles too, so any cell beside that is not walked on.
        for (var direction = 1; direction < 8; direction += 2)
        {
            var next = MapGeometry.Neighbour(cell, direction);

            if (next < 0 || !map.IsWalkable(next))
                return true;
        }

        return false;
    }

    private static bool InLine(int from, int to)
    {
        var (a, b) = (MapGeometry.PointOf(from), MapGeometry.PointOf(to));

        return from != to && (a.X == b.X || a.Y == b.Y);
    }

    private static bool InDiagonal(int from, int to)
    {
        var (a, b) = (MapGeometry.PointOf(from), MapGeometry.PointOf(to));

        return from != to && Math.Abs(a.X - b.X) == Math.Abs(a.Y - b.Y);
    }

    private static bool InSight(ImaginedFight fight, Combatant actor)
    {
        var occupied = default(CellSet);

        foreach (var fighter in fight.Fighters)
        {
            if (fighter.IsAlive && fighter.Cell is not Combatant.NoCell)
                occupied.Add(fighter.Cell);
        }

        foreach (var enemy in fight.Fighters)
        {
            if (enemy.IsAlive && enemy.IsVisible && enemy.Team != actor.Team && enemy.Cell is not Combatant.NoCell && LineOfSight.IsClear(fight.Map, occupied, enemy.Cell, actor.Cell))
                return true;
        }

        return false;
    }

    /// <summary>Gets the challenge once a plan of the turn kept it: kept at any price for the rest of the turn.</summary>
    /// <returns>The challenge, its price without bound.</returns>
    /// <remarks>
    /// The review's finding (D464): weighed again at each of the turn's searches, the price falls with what is left of the
    /// turn to gain, so that the walk that ends a turn kept so far would leave it, the turn paid for nothing.
    /// </remarks>
    public TurnChallenge KeptThisTurn()
    {
        return this with { MostLoss = double.PositiveInfinity };
    }

    /// <summary>Tells whether the fighter playing may cast a level now and keep the challenge.</summary>
    /// <param name="fight">The fight as it stands.</param>
    /// <param name="spellId">The level's spell.</param>
    /// <returns>Whether the cast keeps it.</returns>
    public bool AllowsCast(ImaginedFight fight, int spellId)
    {
        ArgumentNullException.ThrowIfNull(fight);

        return !EachActionOnce || fight.History.CastsIn(spellId, fight.Round) is 0;
    }

    /// <summary>Tells whether the fighter playing may walk on its way to a cast, holding some movement points once there.</summary>
    /// <param name="fight">The fight as it stands.</param>
    /// <param name="knowledge">What the AI knows, the points the fighter began its turn with.</param>
    /// <param name="movementPointsLeft">The movement points left once it walked.</param>
    /// <returns>Whether the walk keeps the challenge.</returns>
    public bool AllowsWalk(ImaginedFight fight, FightKnowledge knowledge, int movementPointsLeft)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(knowledge);

        return Movement switch
        {
            TurnMovement.Still => false,
            TurnMovement.OnePoint => Used(fight, knowledge, movementPointsLeft) <= 1,
            _ => true,
        };
    }

    /// <summary>Tells whether a turn ended as a fight stands keeps the challenge.</summary>
    /// <param name="fight">The fight as the turn ends.</param>
    /// <param name="knowledge">What the AI knows, the points the fighter began its turn with.</param>
    /// <returns>Whether the challenge is kept.</returns>
    public bool IsKept(ImaginedFight fight, FightKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(knowledge);

        var actor = fight.Actor;

        if (!actor.IsAlive || actor.Cell is Combatant.NoCell)
            return true;

        if (EachActionOnce && fight.History.Round == fight.Round && fight.History.ThisTurn.Values.Any(count => count > 1))
            return false;

        var used = Used(fight, knowledge, actor.MovementPoints);
        var moved = Movement switch
        {
            TurnMovement.Still => used <= 0 && (StartCell is not { } start || actor.Cell == start),
            TurnMovement.OnePoint => used == 1,
            TurnMovement.AllPoints => actor.MovementPoints <= 0,
            _ => true,
        };

        return moved && Ends(fight, actor);
    }

    private bool Ends(ImaginedFight fight, Combatant actor)
    {
        return End switch
        {
            TurnEnd.BesideAnAlly => Others(fight, actor, ally: true).Any(cell => MapGeometry.Distance(actor.Cell, cell) is 1),
            TurnEnd.AwayFromAllies => !Others(fight, actor, ally: true).Any(cell => MapGeometry.Distance(actor.Cell, cell) is 1),
            TurnEnd.AwayFromEnemies => !Others(fight, actor, ally: false).Any(cell => MapGeometry.Distance(actor.Cell, cell) is 1),
            TurnEnd.BesideAnObstacle => BesideAnObstacle(fight.Map, actor.Cell),
            TurnEnd.InLineWithAnAlly => Others(fight, actor, ally: true).Any(cell => InLine(actor.Cell, cell)),
            TurnEnd.InDiagonalWithAnAlly => Others(fight, actor, ally: true).Any(cell => InDiagonal(actor.Cell, cell)),
            TurnEnd.InSightOfAnEnemy => InSight(fight, actor),
            TurnEnd.OutOfEnemiesSight => !InSight(fight, actor),
            _ => true,
        };
    }
}
