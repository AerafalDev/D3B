namespace D3B.App.ViewModels.Assets;

/// <summary>What a row of the assets' tree is (D326).</summary>
internal enum AssetRowKind
{
    /// <summary>A folder of the project, folded or not.</summary>
    Folder,

    /// <summary>An asset, of the project or given with D3B.</summary>
    Asset,

    /// <summary>A file of the project that does not read, in red, the reason in its tooltip.</summary>
    Problem,

    /// <summary>The heading of the assets given with D3B, read only.</summary>
    Given,
}
