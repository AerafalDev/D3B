namespace D3B.Bot.Harvesting;

/// <summary>A zone of the harvest's rotation, with the tour the activity makes through it (D391).</summary>
/// <remarks>Each zone keeps its own tour, so that the maps gone through and the ones growing back are still there when the rotation comes back to it.</remarks>
internal sealed class HarvestRotationZone
{
    /// <summary>Initializes a new instance of the <see cref="HarvestRotationZone"/> class.</summary>
    /// <param name="subAreaId">The sub-area the activity chose, or <see langword="null"/> for the settings' own zone.</param>
    /// <param name="zone">The zone.</param>
    /// <param name="tour">The tour through it.</param>
    public HarvestRotationZone(int? subAreaId, HarvestZone zone, HarvestTour tour)
    {
        SubAreaId = subAreaId;
        Zone = zone;
        Tour = tour;
    }

    /// <summary>Gets the sub-area the activity chose, or <see langword="null"/> for the settings' own zone.</summary>
    public int? SubAreaId { get; }

    /// <summary>Gets the zone.</summary>
    public HarvestZone Zone { get; }

    /// <summary>Gets the tour through the zone.</summary>
    public HarvestTour Tour { get; }

    /// <summary>Gets or sets when the activity last left the zone for another; the rotation goes next to the one left longest ago.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets when a tour that harvested nothing may be made again; before, the zone is left to grow back.</summary>
    public DateTimeOffset? DryUntil { get; set; }

    /// <summary>Gets or sets whether the activity left the zone since the rotation was drawn: once every zone was, it is drawn again.</summary>
    public bool Done { get; set; }
}
