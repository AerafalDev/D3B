namespace D3B.App.ViewModels.Bottom;

/// <summary>What the dot of a trace's line says of its state (D327).</summary>
internal enum TraceMark
{
    /// <summary>A state the behavior played: green.</summary>
    Playing,

    /// <summary>The layer Always, or no state at all: grey.</summary>
    Always,

    /// <summary>An urgent state cutting the one playing: red.</summary>
    Cut,
}
