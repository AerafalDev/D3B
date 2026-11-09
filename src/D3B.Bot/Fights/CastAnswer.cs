namespace D3B.Bot.Fights;

/// <summary>What the server answered to a cast the AI requested.</summary>
internal enum CastAnswer
{
    /// <summary>The server cast it.</summary>
    Cast,

    /// <summary>The server refused it with an error text, 29 ms after the request: what it refuses depends on what it sees now.</summary>
    Refused,

    /// <summary>The server did not answer within the delay: a spell the character cannot cast at all, such as a transformed character's own.</summary>
    Unanswered,
}
