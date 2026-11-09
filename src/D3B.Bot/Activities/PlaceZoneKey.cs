namespace D3B.Bot.Activities;

/// <summary>Tells a preview what a zone place held when it was reckoned, so that a zone changed in the project is reckoned again (D415).</summary>
internal static class PlaceZoneKey
{
    /// <summary>Gets what the zone holds, in a text that differs when its maps do.</summary>
    /// <param name="zones">The project's zones.</param>
    /// <param name="zone">The zone's id.</param>
    /// <param name="used">Whether the settings' place is a zone; else nothing is read.</param>
    /// <returns>A text for a preview's key.</returns>
    public static string Of(IPlaceZones? zones, string zone, bool used)
    {
        if (!used)
            return "";

        if (zones is null || !zones.TryResolve(zone, out var maps))
            return $"{zone}:gone";

        var hash = new HashCode();

        foreach (var map in maps.Order())
            hash.Add(map);

        return $"{zone}:{maps.Count}:{hash.ToHashCode()}";
    }
}
