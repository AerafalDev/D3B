namespace D3B.App.ViewModels.Studio;

/// <summary>What the search looks among, chosen by a click, Tab, or the query's first sign (D328).</summary>
internal enum SearchScope
{
    /// <summary>Everything, the recent items first while nothing is typed.</summary>
    All,

    /// <summary>The project's assets and those given with D3B.</summary>
    Assets,

    /// <summary>The characters, <c>@</c> first.</summary>
    Characters,

    /// <summary>The maps, by coordinates or name, each shown in preview in the chosen character's tab (D334).</summary>
    Maps,

    /// <summary>The studio's commands and their shortcuts, <c>&gt;</c> first.</summary>
    Commands,

    /// <summary>The settings, each opening its section.</summary>
    Settings,
}
