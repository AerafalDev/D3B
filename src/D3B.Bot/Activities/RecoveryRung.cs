namespace D3B.Bot.Activities;

/// <summary>A rung of the recovery ladder, what is done before an action that failed is tried again (D518).</summary>
public enum RecoveryRung
{
    /// <summary>Nothing more than the step's own pause: the action is tried again from the state as it is.</summary>
    Retry,

    /// <summary>The character is put back in order as after an interruption: a use let end, a walk settled, a dialog left, a walk the server holds let go by a walk (D89, D456).</summary>
    PutInOrder,

    /// <summary>The character walks to a free neighbour of its cell, whatever the state says the server holds (D464).</summary>
    StepAside,

    /// <summary>The character leaves its map by the exit nearest to it, so that the action is tried from another map, whose information the client asks for itself.</summary>
    LeaveMap,
}
