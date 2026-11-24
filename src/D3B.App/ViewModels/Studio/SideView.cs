namespace D3B.App.ViewModels.Studio;

/// <summary>A view of the side bar, an entry of the activity bar (D513).</summary>
internal enum SideView
{
    /// <summary>The characters, online then offline, each with its state and what it does.</summary>
    Characters,

    /// <summary>The library: the project's profiles, routes, zones and lists of steps, then those given with D3B.</summary>
    Library,

    /// <summary>The world: the routes with their steps and the zones, each with an eye.</summary>
    World,
}
