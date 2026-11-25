namespace D3B.Bot.Dungeons;

/// <summary>The dungeon activity's settings: which dungeons, and how the character goes there (D520, D524).</summary>
public sealed class DungeonSettings
{
    /// <summary>Gets or sets the dungeons, by their ids in the client's table, entered in turn in this order (D524).</summary>
    public IReadOnlyList<int> Dungeons { get; set; } = [];

    /// <summary>Gets or sets how many runs the activity makes, a dungeon left by its exit each; 0 goes on until no dungeon of the profile can be entered (D526).</summary>
    public int Runs { get; set; }

    /// <summary>Gets or sets the share of its life, in percent, the character regenerates to before each room's fight (D525).</summary>
    public int LifeShare { get; set; } = 80;

    /// <summary>Gets or sets whether the journey to the dungeon's door may teleport to a zaap.</summary>
    public bool UseZaaps { get; set; } = true;

    /// <summary>Gets or sets whether a failed or stalled run is put in order and restarted (D519).</summary>
    public bool RestartAfterFailure { get; set; } = true;
}
