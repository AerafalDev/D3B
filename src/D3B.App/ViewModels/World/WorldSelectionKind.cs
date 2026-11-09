namespace D3B.App.ViewModels.World;

/// <summary>What is selected in the world: nothing, a character, a route or a zone (D397).</summary>
internal enum WorldSelectionKind
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>A character.</summary>
    Character,

    /// <summary>A route, and perhaps one of its steps.</summary>
    Route,

    /// <summary>A zone.</summary>
    Zone,
}
