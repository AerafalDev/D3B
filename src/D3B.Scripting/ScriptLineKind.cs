namespace D3B.Scripting;

/// <summary>What a line of a script's console says.</summary>
public enum ScriptLineKind
{
    /// <summary>A message the script printed.</summary>
    Message,

    /// <summary>A success the script told.</summary>
    Success,

    /// <summary>An error the script told, or the one that ended it.</summary>
    Error,
}
