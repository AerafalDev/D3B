namespace D3B.Bot.Activities;

/// <summary>Where an activity stands in a map it goes through.</summary>
public enum ZoneMapState
{
    /// <summary>Still to go through.</summary>
    ToVisit,

    /// <summary>Gone through.</summary>
    Visited,

    /// <summary>Seen holding nothing the activity wants: left out.</summary>
    Empty,

    /// <summary>Not reached: the way there failed.</summary>
    Unreachable,
}
