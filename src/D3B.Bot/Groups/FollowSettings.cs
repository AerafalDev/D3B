namespace D3B.Bot.Groups;

/// <summary>The "Follow" activity's settings: the master (D430), and whether the follower fights as a mule (D579).</summary>
public sealed class FollowSettings
{
    /// <summary>Gets or sets the master, a character of the accounts page by its key's text (<see cref="Characters.CharacterKey.ToId"/>); empty while none is chosen.</summary>
    public string Master { get; set; } = "";

    /// <summary>Gets or sets whether the follower fights as a mule: placed, ready, each turn ended at once, its master's AI counting it as no fighting ally (D579).</summary>
    public bool Mule { get; set; }
}
