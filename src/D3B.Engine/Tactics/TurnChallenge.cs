using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Tactics;

/// <summary>A challenge the fighter playing keeps on its turn: its movement points, its casts, where it ends them (D466), what it strikes and kills (D497).</summary>
/// <remarks>
/// <para>
/// The owner's list (D466), read from the client's completion criteria (client.md): Zombie, Statue, Nomade, Versatile,
/// Collant, Misanthrope, Prudent, Raser les murs, De la même lignée, La diagonale du plein, En ligne de mire and Pas vu pas
/// pris, 49% of the 2,976 fights captured; then (D497) Hardi, Élémentaire, Sans-cœur, Premier, Dernier, Élitiste, Tueur à
/// gages, Tueur à vue, Cruel, Ordonné and Focus, which <see cref="Cast"/> judges cast by cast. A search keeps it
/// (<see cref="FightTactics.Plan"/>) only while the turn it costs stays under <see cref="MostLoss"/>.
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

    /// <summary>Gets which enemies the fighter playing may kill.</summary>
    public ChallengeKills Kills { get; init; }

    /// <summary>Gets which enemies the fighter playing may strike.</summary>
    public ChallengeStrikes Strikes { get; init; }

    /// <summary>Gets the enemy the server named the challenge's target, null when it names none.</summary>
    public long? TargetId { get; init; }

    /// <summary>Gets whether the fighter strikes in one element the whole fight, the one its first blow struck in: Élémentaire.</summary>
    public bool SameElement { get; init; }

    /// <summary>Gets the element the fighter's first blow bound it to, null before it struck.</summary>
    public EffectElement? Element { get; init; }

    /// <summary>Gets whether the fighter heals no ally but itself: Sans-cœur.</summary>
    public bool NoHealOnAllies { get; init; }

    /// <summary>Gets the enemy Focus holds the fighters of the side to, struck and alive; null when none is.</summary>
    public long? FocusId { get; init; }

    /// <summary>Gets the cell the fighter playing began its turn on, which Statue's turn ends on; null when not known.</summary>
    /// <remarks>A jump, a teleport or a push of its own spell moves the fighter with its movement points whole.</remarks>
    public int? StartCell { get; init; }

    /// <summary>Gets the most of the turn's gain the challenge may cost, from 0 to 1, or more once the turn played for it.</summary>
    /// <remarks>
    /// The owner's choice (D497, after D466's half): three quarters of what keeps the reward a minute the same. A challenge won
    /// adds its bonus b to the fight's reward, so a fight slower by a share l of its pace still earns as much a minute while
    /// l ≤ b / (1 + b); less than all of it, since a challenge is lost at the first turn that breaks it, so that a costly turn
    /// may be paid for nothing.
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
            "TD<2,hc0,e1" => new TurnChallenge { End = TurnEnd.BesideAnEnemy },
            "TD<2,hc0,whb" => new TurnChallenge { End = TurnEnd.BesideAnObstacle },
            "SP=te,l,hc0,hc0" => new TurnChallenge { End = TurnEnd.InLineWithAnAlly },
            "SP=te,d,hc0,hc0" => new TurnChallenge { End = TurnEnd.InDiagonalWithAnAlly },
            "SV=te,hc0,e1,s1,a" => new TurnChallenge { End = TurnEnd.InSightOfAnEnemy },
            "SV=te,hc0,e1,s0,v" => new TurnChallenge { End = TurnEnd.OutOfEnemiesSight },
            "CK~1" => new TurnChallenge { Kills = ChallengeKills.TargetFirst },
            "Ck~1" => new TurnChallenge { Kills = ChallengeKills.TargetLast },
            "SL<1" => new TurnChallenge { Kills = ChallengeKills.LowestLevelFirst },
            "SL>1" => new TurnChallenge { Kills = ChallengeKills.HighestLevelFirst },
            "CC=e0,m1,k,k" => new TurnChallenge { Kills = ChallengeKills.TargetOnly },
            "SC=hc0,e,s" => new TurnChallenge { SameElement = true },
            "SO!e0,hc0,h" => new TurnChallenge { NoHealOnAllies = true },
            "CK~!,1" => new TurnChallenge { Strikes = ChallengeStrikes.TargetOnly },
            "SF=1" => new TurnChallenge { Strikes = ChallengeStrikes.OneAtATime },
            "CO=e0,m1,tk,n" => new TurnChallenge { Strikes = ChallengeStrikes.NearestOnly },
            _ => null,
        };

        var share = Math.Max(0, bonus) / 100.0;

        return challenge is null ? null : challenge with { MostLoss = 0.75 * share / (1 + share) };
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
        // The client's description (D466): holes and the map's edges are obstacles too, so any cell beside that is not walked on.
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
    /// The review's finding (D466): weighed again at each of the turn's searches, the price falls with what is left of the
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

    /// <summary>Gets the challenge once the fighter playing cast: as it stands after the cast, or null when the cast broke it.</summary>
    /// <param name="before">The fight before the cast, the fighter on the cell it casts from.</param>
    /// <param name="after">The fight once the cast is imagined.</param>
    /// <param name="elements">The elements the cast strikes in.</param>
    /// <returns>The challenge after the cast; null when broken.</returns>
    public TurnChallenge? Cast(ImaginedFight before, ImaginedFight after, IReadOnlyCollection<EffectElement> elements)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(elements);

        var killed = new List<Combatant>();
        var struck = new List<Combatant>();

        foreach (var enemy in Enemies(before))
        {
            var now = after.Find(enemy.Id);

            if (now is not { IsAlive: true })
                killed.Add(enemy);

            if (now is not { IsAlive: true } || now.Life < enemy.Life)
                struck.Add(enemy);
        }

        if (!KillsAllowed(before, killed) || !StrikesAllowed(before, struck) || (NoHealOnAllies && HealsAnAlly(before, after)))
            return null;

        // Bubble's reading, the client's text alike: neutral is an element, and a cast striking in two breaks it.
        if (SameElement && struck.Count > 0 && elements.Count > 0)
        {
            var element = elements.First();

            if (elements.Count > 1 || (Element is { } bound && bound != element))
                return null;

            return this with { Element = element };
        }

        // Focus holds the side to the enemy struck until it is dead.
        if (Strikes is ChallengeStrikes.OneAtATime)
        {
            var focus = FocusId is { } id && before.Find(id) is { IsAlive: true } ? id : struck.Count > 0 ? struck[0].Id : (long?)null;

            return this with { FocusId = focus is { } held && after.Find(held) is { IsAlive: true } ? held : null };
        }

        // Tueur à vue's next target is the enemy nearest the fighter once its target is dead, as the server names it.
        if (Strikes is ChallengeStrikes.NearestOnly && TargetId is { } target && after.Find(target) is not { IsAlive: true })
            return this with { TargetId = Nearest(after)?.Id };

        return this;
    }

    private bool StrikesAllowed(ImaginedFight before, List<Combatant> struck)
    {
        if (Strikes is ChallengeStrikes.Free || struck.Count is 0)
            return true;

        if (Strikes is ChallengeStrikes.OneAtATime)
            return FocusId is { } focus && before.Find(focus) is { IsAlive: true } ? struck.TrueForAll(enemy => enemy.Id == focus) : struck.Count is 1;

        var target = TargetId is { } id && before.Find(id) is { IsAlive: true } living ? living : null;

        if (target is null && Strikes is ChallengeStrikes.NearestOnly)
            target = Nearest(before);

        return target is null || struck.TrueForAll(enemy => enemy.Id == target.Id);
    }

    private static bool HealsAnAlly(ImaginedFight before, ImaginedFight after)
    {
        var actor = before.Actor;

        foreach (var ally in before.Fighters)
        {
            if (ally.Id != actor.Id && ally.Team == actor.Team && ally.IsAlive && after.Find(ally.Id) is { } now && now.Life > ally.Life)
                return true;
        }

        return false;
    }

    private static Combatant? Nearest(ImaginedFight fight)
    {
        var cell = fight.Actor.Cell;

        return cell is Combatant.NoCell ? null : Enemies(fight).Where(enemy => enemy.Cell is not Combatant.NoCell).MinBy(enemy => MapGeometry.Distance(cell, enemy.Cell));
    }

    private bool KillsAllowed(ImaginedFight before, List<Combatant> killed)
    {
        if (killed.Count is 0)
            return true;

        // The enemies' summons are left out, as Bubble leaves them, the kills' order asking of the fight's own enemies.
        killed = killed.FindAll(enemy => enemy.SummonerId is null);

        if (killed.Count is 0)
            return true;

        // The enemies a cast kills at once are killed in an order the server chooses: each is allowed when some order is.
        var survivors = Enemies(before).Where(enemy => enemy.SummonerId is null && !killed.Exists(dead => dead.Id == enemy.Id)).ToList();

        switch (Kills)
        {
            case ChallengeKills.LowestLevelFirst:
                return survivors.Count is 0 || killed.Max(enemy => enemy.Level) <= survivors.Min(enemy => enemy.Level);
            case ChallengeKills.HighestLevelFirst:
                return survivors.Count is 0 || killed.Min(enemy => enemy.Level) >= survivors.Max(enemy => enemy.Level);
        }

        var named = TargetId is { } id && before.Find(id) is { IsAlive: true };

        if (Kills is ChallengeKills.TargetOnly)
            return named && killed.TrueForAll(enemy => enemy.Id == TargetId);

        // A target the server named that is gone asks nothing more.
        if (TargetId is not { } target || before.Find(target) is not { IsAlive: true })
            return true;

        return Kills switch
        {
            ChallengeKills.TargetFirst => killed.Exists(enemy => enemy.Id == target),
            ChallengeKills.TargetLast => !killed.Exists(enemy => enemy.Id == target) || survivors.Count is 0,
            _ => true,
        };
    }

    private static IEnumerable<Combatant> Enemies(ImaginedFight fight)
    {
        var team = fight.Actor.Team;

        return fight.Fighters.Where(fighter => fighter.Team != team && fighter.IsAlive);
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
            TurnEnd.BesideAnEnemy => Others(fight, actor, ally: false).Any(cell => MapGeometry.Distance(actor.Cell, cell) is 1),
            TurnEnd.BesideAnObstacle => BesideAnObstacle(fight.Map, actor.Cell),
            TurnEnd.InLineWithAnAlly => Others(fight, actor, ally: true).Any(cell => InLine(actor.Cell, cell)),
            TurnEnd.InDiagonalWithAnAlly => Others(fight, actor, ally: true).Any(cell => InDiagonal(actor.Cell, cell)),
            TurnEnd.InSightOfAnEnemy => InSight(fight, actor),
            TurnEnd.OutOfEnemiesSight => !InSight(fight, actor),
            _ => true,
        };
    }
}
