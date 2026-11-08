using Avalonia.Media;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Assets;

/// <summary>Each kind's icon and colours, the same everywhere an asset shows (D326).</summary>
/// <remarks>
/// Behavior blue, profile violet, route teal, zone green, list amber, planning light blue, fight preferences red, as the
/// mockup's; the icons are IntelliJ's, painted in the kind's colour on its dark tint.
/// </remarks>
internal static class AssetKinds
{
    /// <summary>Gets the kinds in the order the Add menu offers them.</summary>
    public static IReadOnlyList<AssetKind> All { get; } = [AssetKind.Behavior, AssetKind.Profile, AssetKind.List, AssetKind.Route, AssetKind.Zone, AssetKind.Planning, AssetKind.FightPreferences];

    /// <summary>Gets a kind's IntelliJ icon by its key.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The key, such as <c>Nodes.Template</c>.</returns>
    public static string IconOf(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Behavior => "Toolwindows.Hierarchy",
            AssetKind.Profile => "Nodes.Template",
            AssetKind.Route => "General.Vcs",
            AssetKind.Zone => "Actions.SelectAll",
            AssetKind.List => "General.ListFiles",
            AssetKind.Planning => "Actions.StopWatch",
            _ => "Nodes.SecurityRole",
        };
    }

    /// <summary>Gets a kind's colour, its icon's.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The colour.</returns>
    public static Color ColorOf(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Behavior => Color.Parse("#7FA7FF"),
            AssetKind.Profile => Color.Parse("#C9A7F2"),
            AssetKind.Route => Color.Parse("#5FD3C0"),
            AssetKind.Zone => Color.Parse("#A3D47B"),
            AssetKind.List => Color.Parse("#E9B04F"),
            AssetKind.Planning => Color.Parse("#8FB8F2"),
            _ => Color.Parse("#F3A19A"),
        };
    }

    /// <summary>Gets a kind's dark tint, its icon's square.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The colour.</returns>
    public static Color TintOf(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Behavior => Color.Parse("#1F2A3D"),
            AssetKind.Profile => Color.Parse("#2A2238"),
            AssetKind.Route => Color.Parse("#172B29"),
            AssetKind.Zone => Color.Parse("#1F2A1B"),
            AssetKind.List => Color.Parse("#2D2516"),
            AssetKind.Planning => Color.Parse("#1F2A38"),
            _ => Color.Parse("#382422"),
        };
    }

    /// <summary>Gets the session that makes a kind's editor, for the kinds D3B cannot make yet.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The session, such as <c>7d</c>, or <see langword="null"/> for a kind made today.</returns>
    public static string? SessionOf(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Profile or AssetKind.Route or AssetKind.Behavior => null,
            AssetKind.Zone => "7d3",
            AssetKind.List or AssetKind.Planning => "7f",
            _ => "7h",
        };
    }
}
