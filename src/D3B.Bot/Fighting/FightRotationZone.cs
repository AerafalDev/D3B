namespace D3B.Bot.Fighting;

/// <summary>A ground of the fight's rotation, with the maps the activity went through in it (D391).</summary>
/// <remarks>Each ground keeps its own maps gone through, so that the rotation finds them as it left them.</remarks>
internal sealed class FightRotationZone
{
    /// <summary>Initializes a new instance of the <see cref="FightRotationZone"/> class.</summary>
    /// <param name="subAreaId">The sub-area the activity chose, or <see langword="null"/> for the settings' own ground.</param>
    /// <param name="ground">The ground.</param>
    public FightRotationZone(int? subAreaId, HuntingGround ground)
    {
        SubAreaId = subAreaId;
        Ground = ground;
    }

    /// <summary>Gets the sub-area the activity chose, or <see langword="null"/> for the settings' own ground.</summary>
    public int? SubAreaId { get; }

    /// <summary>Gets the ground.</summary>
    public HuntingGround Ground { get; }

    /// <summary>Gets the maps of the ground gone through in this tour.</summary>
    public HashSet<long> Visited { get; } = [];

    /// <summary>Gets the maps the way to failed, left out of the tours.</summary>
    public HashSet<long> Unreachable { get; } = [];

    /// <summary>Gets or sets when the activity last left the ground for another; the rotation goes next to the one left longest ago.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets whether the activity went through the ground since the rotation was drawn: once every ground was, it is drawn again.</summary>
    public bool Done { get; set; }
}
