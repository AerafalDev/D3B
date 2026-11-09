namespace D3B.App.ViewModels.World;

/// <summary>The tools of the world viewport's toolbar (D397).</summary>
internal enum WorldTool
{
    /// <summary>Selects what is pressed: a character, a step, a zone; moves a step; pans.</summary>
    Select,

    /// <summary>Traces the selected route, or a new one: a press adds a step.</summary>
    Route,

    /// <summary>Paints the selected zone, or a new one.</summary>
    Zone,

    /// <summary>Records a route while a character plays it, once 7d4b makes it (D394).</summary>
    Record,
}
