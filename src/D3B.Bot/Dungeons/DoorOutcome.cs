namespace D3B.Bot.Dungeons;

/// <summary>How a talk at a dungeon's door or exit went (<see cref="DungeonDoors"/>).</summary>
internal enum DoorOutcome
{
    /// <summary>The character is through: in a room of the dungeon, or out of it.</summary>
    Through,

    /// <summary>The character left the map, but not for a room of the dungeon.</summary>
    Elsewhere,

    /// <summary>An NPC answered nothing or could not be talked to, and none let the character through: it may be tried again.</summary>
    Unanswered,

    /// <summary>Every NPC answered, and none let the character through.</summary>
    Refused,
}
