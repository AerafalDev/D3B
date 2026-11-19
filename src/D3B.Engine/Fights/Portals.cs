using System.Collections.Immutable;
using D3B.Engine.Maps;

namespace D3B.Engine.Fights;

/// <summary>An Eliotrope's portals: the way a cast on one goes through them, and how many a team keeps.</summary>
/// <remarks>
/// <para>
/// The references' model (D3, D466), Dofus 2's, which no capture of Dofus 3 measured yet: none of the 137 game connections
/// of 3.7.13.17 and 3.7.14.18 holds a cast the server says went through a portal, among 24 portals set. A portal is a mark of
/// effect 1181 on one cell, kept the whole fight; a team keeps four, a fifth taking the oldest's place. A portal is open while
/// no fighter stands on it and its team has another open. A cast on an open portal goes along its chain, from it to the
/// nearest of its team's other open portals, then to the nearest of those left, and so on, and comes out of the last one,
/// its target as far from that exit, in the same direction, as the caster is from the portal cast on; its effects gain, in
/// percent, the first dice of each portal's effect 1181, 2 in the pack, for each cell between it and the portal before it in
/// the chain, the effect's text naming its third, 0, a share of its own. Any fighter's cast goes through, the references asking no team.
/// </para>
/// <para>
/// Not modelled: a tie between two portals as near, which the references break by an angle, here by the older; a portal used
/// by a walk, which closes it until the next turn; one an effect 1183 closes; the walk through one (1182).
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
        CellMark? entry = null;

        foreach (var mark in marks)
        {
            if (mark.Kind is CellMarkKind.Portal && mark.Centre == cell)
                entry = mark;
        }

        if (entry is null || occupied.Contains(cell))
            return null;

        var left = new List<CellMark>();

        foreach (var mark in marks)
        {
            if (mark.Kind is CellMarkKind.Portal && mark.Team == entry.Team && mark.Id != entry.Id && !occupied.Contains(mark.Centre))
                left.Add(mark);
        }

        if (left.Count is 0)
            return null;

        var current = entry;
        var bonus = 0;

        while (left.Count > 0)
        {
            var next = left[0];

            foreach (var mark in left)
            {
                if (MapGeometry.Distance(current.Centre, mark.Centre) < MapGeometry.Distance(current.Centre, next.Centre))
                    next = mark;
            }

            bonus += next.SpellId * MapGeometry.Distance(current.Centre, next.Centre);
            left.Remove(next);
            current = next;
        }

        var from = MapGeometry.PointOf(casterCell);
        var at = MapGeometry.PointOf(cell);
        var exit = MapGeometry.PointOf(current.Centre);

        return MapGeometry.TryGetCell(new MapPoint(exit.X + at.X - from.X, exit.Y + at.Y - from.Y), out var target)
            ? new PortalPassage(cell, current.Centre, target, bonus)
            : null;
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
