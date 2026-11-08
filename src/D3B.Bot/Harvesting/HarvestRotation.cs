namespace D3B.Bot.Harvesting;

/// <summary>Which zone of the harvest's rotation comes next, and when to wake if none can be gone through yet (D391).</summary>
internal static class HarvestRotation
{
    /// <summary>Finds the zone to go through next: of those with maps to go through now and not swept for nothing a moment ago, the one left longest ago.</summary>
    /// <param name="zones">The rotation, in the order of the draw.</param>
    /// <param name="now">The moment.</param>
    /// <returns>The zone; <see langword="null"/> when every one still grows back or was swept for nothing, which the activity waits for.</returns>
    public static HarvestRotationZone? Next(IReadOnlyList<HarvestRotationZone> zones, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zones);

        return zones
            .Select((zone, index) => (zone, index))
            .Where(pair => !(pair.zone.DryUntil > now) && pair.zone.Tour.Remaining(pair.zone.Zone).Count > 0)
            .OrderBy(pair => pair.zone.CompletedAt ?? DateTimeOffset.MinValue)
            .ThenBy(pair => pair.index)
            .Select(pair => pair.zone)
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
