namespace D3B.App.ViewModels.World;

/// <summary>What the Outliner lists: the side bar shows its characters and its world apart, as two views of the activity bar (D513).</summary>
internal enum OutlinerScope
{
    /// <summary>The characters, the routes and the zones.</summary>
    All,

    /// <summary>The characters alone.</summary>
    Characters,

    /// <summary>The routes with their steps and the zones.</summary>
    World,
}
