namespace D3B.Bot.Activities;

/// <summary>The sub-areas an activity that chooses its place goes through in turn, drawn from the richest the character can reach (D391).</summary>
/// <remarks>
/// <para>
/// The activity chose one sub-area for a character and, a tour done, chose it again: the ranking moves only with a level, so
/// the character did the same maps again while the resources grew back. The rotation draws several, which the activity goes
/// through one after the other.
/// </para>
/// <para>
/// The pool is the <see cref="PoolSize"/> richest sub-areas the character can reach, those worth under a quarter of the richest
/// left out so that a rotation does not send the character far for a few maps. Of the <c>count</c> sub-areas drawn, up to half
/// are given (the zones of each profession's best resource, which the activity names), the others drawn at random from the pool,
/// the richer likelier, none ignored. A count of 1 is the richest alone, as before.
/// </para>
/// </remarks>
public static class PlaceRotation
{
    /// <summary>How many of the richest reachable sub-areas the draw is made from.</summary>
    public const int PoolSize = 10;

    /// <summary>The share of the richest sub-area's worth under which a sub-area is left out of the pool.</summary>
    public const double LeastShare = 0.25;

    /// <summary>Draws the sub-areas to go through.</summary>
    /// <param name="ranked">The sub-areas the character may go to, the richest first; those not reachable are passed over, the next taking their place.</param>
    /// <param name="given">The sub-areas the rotation keeps whatever the draw, the first first; they are kept even when outside the pool, up to half the count.</param>
    /// <param name="count">How many sub-areas to go through, 1 to <see cref="PoolSize"/>.</param>
    /// <param name="reachable">Whether the character can travel to a sub-area.</param>
    /// <param name="random">The source of the draw.</param>
    /// <returns>The sub-areas, the given ones first; fewer than <paramref name="count"/> when the pool holds fewer; none when nothing is reachable.</returns>
    public static IReadOnlyList<int> Draw(IEnumerable<RotationCandidate> ranked, IEnumerable<int> given, int count, Func<int, bool> reachable, Random random)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        ArgumentNullException.ThrowIfNull(given);
        ArgumentNullException.ThrowIfNull(reachable);
        ArgumentNullException.ThrowIfNull(random);

        count = Math.Clamp(count, 1, PoolSize);

        var pool = new List<RotationCandidate>();
        double? least = null;

        foreach (var candidate in ranked)
        {
            if (pool.Count >= PoolSize)
                break;

            if (least is { } floor && candidate.Score < floor)
                continue;

            if (!reachable(candidate.SubAreaId))
                continue;

            least ??= candidate.Score * LeastShare;
            pool.Add(candidate);
        }

        if (pool.Count is 0)
            return [];

        if (count is 1)
            return [pool[0].SubAreaId];

        var chosen = new List<int>();

        foreach (var id in given)
        {
            if (chosen.Count >= (count + 1) / 2)
                break;

            if (!chosen.Contains(id) && reachable(id))
                chosen.Add(id);
        }

        var rest = pool.Where(candidate => !chosen.Contains(candidate.SubAreaId)).ToList();

        while (chosen.Count < count && rest.Count > 0)
        {
            var index = IndexOf(rest, random);

            chosen.Add(rest[index].SubAreaId);
            rest.RemoveAt(index);
        }

        return chosen;
    }

    private static int IndexOf(List<RotationCandidate> rest, Random random)
    {
        var total = rest.Sum(candidate => candidate.Score);

        if (total <= 0)
            return random.Next(rest.Count);

        var roll = random.NextDouble() * total;

        for (var index = 0; index < rest.Count; index++)
        {
            roll -= rest[index].Score;

            if (roll < 0)
                return index;
        }

        return rest.Count - 1;
    }
}
