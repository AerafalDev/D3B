namespace D3B.Bot.Groups;

/// <summary>The "Follow" activity's settings: the master, the one setting (D430).</summary>
public sealed class FollowSettings
{
    /// <summary>Gets or sets the master, a character of the accounts page by its key's text (<see cref="Characters.CharacterKey.ToId"/>); empty while none is chosen.</summary>
    public string Master { get; set; } = "";
}
