namespace D3B.App.ViewModels.World;

/// <summary>What a row of the Outliner is (D397).</summary>
internal enum OutlinerRowKind
{
    /// <summary>A heading over a group: characters, routes, zones.</summary>
    Heading,

    /// <summary>A character, online or not.</summary>
    Character,

    /// <summary>A route, which unfolds into its steps.</summary>
    Route,

    /// <summary>A step of a route.</summary>
    Step,

    /// <summary>A zone.</summary>
    Zone,
}
