namespace D3B.Bot.Inventory;

/// <summary>How wearing a stack of the inventory ended (D565).</summary>
public enum ObjectWearOutcome
{
    /// <summary>The stack is worn where asked: moved there, or worn there already.</summary>
    Worn,

    /// <summary>The character carries no such stack: nothing was sent.</summary>
    NotCarried,

    /// <summary>The character is in the middle of something: nothing was sent.</summary>
    Busy,

    /// <summary>The server did not move the stack in time.</summary>
    NoAnswer,
}
