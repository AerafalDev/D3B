using System.Collections.Immutable;
using D3B.Data.Monsters;
using D3B.Engine.Maps;

namespace D3B.Engine.Fights;

/// <summary>An Eliotrope's portals: the way a cast or a move on one goes through them, and how many a team keeps.</summary>
/// <remarks>
/// <para>
/// The references' model (D3, D468), measured on the captures of 3.7.14.18 of 2026-11-18 to 21 (D491). A portal is a mark of
/// effect 1181 on one cell, kept the whole fight; a team keeps four, a fifth taking the oldest's place. A portal is open while
/// no fighter stands on it, no fighter went through it this turn, and its team has another open. A cast on an open portal
/// goes along its chain, from it to the nearest of its team's other open portals, then to the nearest of those left, and so
/// on, and comes out of the last one, its target as far from that exit, in the same direction, as the caster is from the
/// portal cast on: 1,329 casts of 1,329 as the server named them. Its effects gain, in percent, the first dice of each
/// portal's effect 1181, 2 in the pack, for each cell between it and the portal before it in the chain: the share that fits
/// the most damage dealt, where the references' 2 more fit fewer. Any fighter's cast goes through. A walk or a push that ends
/// on an open portal sends its fighter onto the last of the chain, which closes the two until a turn begins.
/// </para>
/// <para>
/// Not modelled: a portal an effect 1183 closes; the spell 14631 that each of an Eliotrope's casts through portals casts on it.
/// </para>
/// </remarks>
public static class Portals
{
    /// <summary>The effect that sets a portal.</summary>
    public const int SetEffect = 1181;

    /// <summary>The effect that closes a portal, whose spells go through none.</summary>
    public const int CloseEffect = 1183;

    /// <summary>The most portals a team keeps.</summary>
    public const int MostByTeam = 4;

    /// <summary>Finds the way a cast on a cell goes through the portals, when the cell is an open portal's.</summary>
    /// <param name="marks">The fight's marks.</param>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="casterCell">The caster's cell.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <returns>The way, or none when the cell is no open portal's, or its target off the map.</returns>
    public static PortalPassage? Through(IReadOnlyList<CellMark> marks, IReadOnlyList<Combatant> fighters, int casterCell, int cell)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(fighters);

        if (casterCell is Combatant.NoCell || !HasPortal(marks))
            return null;

        var occupied = CastRules.OccupiedBy(fighters);

        if (occupied.Contains(cell) || EntryOf(marks, cell) is not { } entry || Chain(marks, occupied, entry) is not { } way)
            return null;

        var from = MapGeometry.PointOf(casterCell);
        var at = MapGeometry.PointOf(cell);
        var exit = MapGeometry.PointOf(way.Exit.Centre);

        return MapGeometry.TryGetCell(new MapPoint(exit.X + at.X - from.X, exit.Y + at.Y - from.Y), out var target)
            ? new PortalPassage(cell, way.Exit.Centre, target, way.Bonus)
            : null;
    }

    /// <summary>Finds where a fighter ends a walk or a push on a cell, which an open portal sends out of the last of its chain.</summary>
    /// <param name="marks">The fight's marks.</param>
    /// <param name="fighters">Every fighter of the fight, the mover's own cell, left or reached, counting for none.</param>
    /// <param name="mover">The fighter that moves.</param>
    /// <param name="cell">The cell its move ends on.</param>
    /// <returns>The cell it ends on, and the marks: the two portals closed when it went through, else the same.</returns>
    /// <remarks>
    /// Measured on 3.7.14.18 (D491): 777 of 818 walks and slides that ended on an open portal, by both teams, were teleported
    /// onto the cell of the portal a cast on it comes out of; none of 152 over a portal on its way; none of 64 onto a portal
    /// one went through that turn. The mover's own cell, left, no longer closes the portal there. A monster goes through when
    /// its properties let it, as the references ask.
    /// </remarks>
    public static (int Cell, ImmutableArray<CellMark> Marks) Moved(ImmutableArray<CellMark> marks, IReadOnlyList<Combatant> fighters, Combatant mover, int cell)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(mover);

        if (!HasPortal(marks) || (mover.MonsterId is not null && (mover.MonsterProperties & MonsterProperties.CanUsePortal) is 0))
            return (cell, marks);

        var occupied = default(CellSet);

        foreach (var fighter in fighters)
        {
            if (fighter.Id != mover.Id && fighter.IsAlive && fighter.Cell is not Combatant.NoCell)
                occupied.Add(fighter.Cell);
        }

        if (occupied.Contains(cell) || EntryOf(marks, cell) is not { } entry || Chain(marks, occupied, entry) is not { Exit: var exit })
            return (cell, marks);

        return (exit.Centre, marks.Replace(entry, entry with { IsUsed = true }).Replace(exit, exit with { IsUsed = true }));
    }

    /// <summary>Gets the marks as a turn begins, which opens again every portal a fighter went through.</summary>
    /// <param name="marks">The fight's marks.</param>
    /// <returns>The marks with no portal closed; the same when none was.</returns>
    public static ImmutableArray<CellMark> Reopened(ImmutableArray<CellMark> marks)
    {
        if (!marks.Any(mark => mark.IsUsed))
            return marks;

        return [.. marks.Select(mark => mark.IsUsed ? mark with { IsUsed = false } : mark)];
    }

    /// <summary>Gets the marks once a portal is set, the oldest of its team's gone when it keeps more than <see cref="MostByTeam"/>.</summary>
    /// <param name="marks">The fight's marks, the oldest first, as the server's ids and the search's casts set them.</param>
    /// <param name="portal">The portal set.</param>
    /// <returns>The marks with it, last.</returns>
    public static ImmutableArray<CellMark> Placed(ImmutableArray<CellMark> marks, CellMark portal)
    {
        ArgumentNullException.ThrowIfNull(portal);

        var kept = marks.Add(portal);
        var count = kept.Count(mark => mark.Kind is CellMarkKind.Portal && mark.Team == portal.Team);

        if (count <= MostByTeam)
            return kept;

        return kept.Remove(kept.First(mark => mark.Kind is CellMarkKind.Portal && mark.Team == portal.Team));
    }

    private static CellMark? EntryOf(IReadOnlyList<CellMark> marks, int cell)
    {
        CellMark? entry = null;

        foreach (var mark in marks)
        {
            if (mark.Kind is CellMarkKind.Portal && mark.Centre == cell)
                entry = mark;
        }

        return entry is { IsUsed: false } ? entry : null;
    }

    private static (CellMark Exit, int Bonus)? Chain(IReadOnlyList<CellMark> marks, in CellSet occupied, CellMark entry)
    {
        var left = new List<CellMark>();

        foreach (var mark in marks)
        {
            if (mark.Kind is CellMarkKind.Portal && mark.Team == entry.Team && mark.Id != entry.Id && !mark.IsUsed && !occupied.Contains(mark.Centre))
                left.Add(mark);
        }

        if (left.Count is 0)
            return null;

        var current = entry;
        var bonus = 0;

        while (left.Count > 0)
        {
            var next = Next(current, left);

            bonus += next.SpellId * MapGeometry.Distance(current.Centre, next.Centre);
            left.Remove(next);
            current = next;
        }

        return (current, bonus);
    }

    private static CellMark Next(CellMark current, List<CellMark> left)
    {
        var nearest = int.MaxValue;
        var count = 0;
        CellMark? next = null;

        foreach (var mark in left)
        {
            var distance = MapGeometry.Distance(current.Centre, mark.Centre);

            if (distance < nearest)
                (nearest, count, next) = (distance, 0, mark);

            if (distance == nearest)
                count++;
        }

        return count is 1 ? next! : Tied(current, [.. left.Where(mark => MapGeometry.Distance(current.Centre, mark.Centre) == nearest)]);
    }

    private static CellMark Tied(CellMark current, List<CellMark> tied)
    {
        // Measured on 3.7.14.18 (D491), as the references break it (D3): of the portals as near, turning from the map's bottom,
        // x falling first, the last of an arc narrower than a half turn, or the first when they lie all around, or two opposite.
        var centre = MapGeometry.PointOf(current.Centre);
        var vectors = tied.ConvertAll(mark => Vector(centre, mark.Centre));
        var order = Enumerable.Range(0, tied.Count).OrderBy(index => Turn(vectors[index])).ToList();

        for (var index = 0; index < order.Count; index++)
        {
            var previous = vectors[order[(index + order.Count - 1) % order.Count]];
            var turned = Determinant(previous, vectors[order[index]]);

            if (turned < 0)
                return tied[order[(index + order.Count - 1) % order.Count]];

            if (turned is 0 && order.Count <= 2)
                break;
        }

        return tied[order[0]];
    }

    private static MapPoint Vector(MapPoint from, int cell)
    {
        var to = MapGeometry.PointOf(cell);

        return new MapPoint(to.X - from.X, to.Y - from.Y);
    }

    private static double Turn(MapPoint vector)
    {
        var angle = Math.Atan2(-vector.X, vector.Y);

        return angle < 0 ? angle + (2 * Math.PI) : angle;
    }

    private static int Determinant(MapPoint first, MapPoint second)
    {
        return (first.X * second.Y) - (first.Y * second.X);
    }

    private static bool HasPortal(IReadOnlyList<CellMark> marks)
    {
        for (var index = 0; index < marks.Count; index++)
        {
            if (marks[index].Kind is CellMarkKind.Portal)
                return true;
        }

        return false;
    }
}
