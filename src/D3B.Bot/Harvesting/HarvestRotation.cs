namespace D3B.Bot.Harvesting;

/// <summary>Which zone of the harvest's rotation comes next, and when to wake if none can be gone through yet (D391, D506).</summary>
internal static class HarvestRotation
{
    /// <summary>
    /// Finds the zone to go through next, of those with maps to go through now and not swept for nothing a moment ago: the nearest of
    /// the round's not yet gone to, else the one left longest ago.
    /// </summary>
    /// <param name="zones">The rotation, in the order of the draw.</param>
    /// <param name="now">The moment.</param>
    /// <param name="left">The zone the character just left, which comes after the others: a new round would otherwise begin with it.</param>
    /// <param name="changesTo">The map changes from where the character stands to a zone's maps left to go through, given with them; <see langword="null"/> when no way is known.</param>
    /// <returns>The zone; <see langword="null"/> when every one still grows back or was swept for nothing, which the activity waits for.</returns>
    /// <remarks>
    /// Measured on the owner's four harvests of 23 November, 14 h each: gone to in the order they were left, a random one each round,
    /// the miner's ten zones across the world cost it 478 minutes of journeys, about as long as it harvested; the nearest of the round
    /// first, 35 % fewer map changes on the same rounds. Each zone still comes once a round, so that two neighbours never take turns
    /// while the others wait, what D391 feared of the nearest zone alone.
    /// </remarks>
    public static HarvestRotationZone? Next(IReadOnlyList<HarvestRotationZone> zones, DateTimeOffset now, HarvestRotationZone? left, Func<HarvestRotationZone, IReadOnlySet<long>, int?> changesTo)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(changesTo);

        return zones
            .Select((zone, index) => (zone, index, remaining: zone.DryUntil > now ? [] : zone.Tour.Remaining(zone.Zone)))
            .Where(entry => entry.remaining.Count > 0)
            .Select(entry => (entry.zone, entry.index, changes: entry.zone.Done ? null : changesTo(entry.zone, entry.remaining)))
            .OrderBy(entry => entry.zone.Done)
            .ThenBy(entry => ReferenceEquals(entry.zone, left))
            .ThenBy(entry => entry.zone.Done ? 0 : entry.changes ?? int.MaxValue)
            .ThenBy(entry => entry.zone.CompletedAt ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.zone)
            .FirstOrDefault();
    }

    /// <summary>Finds when the first zone should be ready to go through again.</summary>
    /// <param name="zones">The rotation.</param>
    /// <param name="now">The moment.</param>
    /// <returns>When a zone's wait for nothing ends or its first map should have grown back; <see langword="null"/> when none is known.</returns>
    public static DateTimeOffset? WakeAt(IReadOnlyList<HarvestRotationZone> zones, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zones);

        DateTimeOffset? first = null;

        foreach (var zone in zones)
        {
            foreach (var at in new[] { zone.DryUntil, zone.Tour.NextReady(zone.Zone) })
            {
                if (at is { } when && when > now && (first is null || when < first))
                    first = when;
            }
        }

        return first;
    }
}
