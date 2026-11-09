namespace D3B.Hooks;

/// <summary>What a guard does with a tool call it does not let through silently.</summary>
internal enum HookDecision
{
    /// <summary>The call is refused: Claude Code gets the reason and nothing runs.</summary>
    Deny,

    /// <summary>The call waits for the owner's approval.</summary>
    Ask,
}
