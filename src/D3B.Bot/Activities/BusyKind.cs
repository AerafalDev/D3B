namespace D3B.Bot.Activities;

/// <summary>What keeps a character from a step's action, as <see cref="BusyWait"/> tells it (D483).</summary>
public enum BusyKind
{
    /// <summary>The character's map is not known yet.</summary>
    MapUnknown,

    /// <summary>The character uses an element, such as a harvest, whose end has not come.</summary>
    Using,

    /// <summary>The character is in a dialog with an NPC or a zaap, or in an exchange with a storage or another character.</summary>
    InDialog,

    /// <summary>The character walks a path the client has not confirmed.</summary>
    Walking,
}
