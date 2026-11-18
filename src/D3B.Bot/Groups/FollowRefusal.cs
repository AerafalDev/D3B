namespace D3B.Bot.Groups;

/// <summary>Why a character cannot follow the master it names (D436).</summary>
public enum FollowRefusal
{
    /// <summary>It names itself.</summary>
    Itself,

    /// <summary>The master follows another: no follower ever leads (D432).</summary>
    MasterFollows,

    /// <summary>The character leads followers of its own.</summary>
    Leads,

    /// <summary>The master leads as many followers as a party holds beside it.</summary>
    Full,

    /// <summary>The character follows a master already.</summary>
    Follows,
}
