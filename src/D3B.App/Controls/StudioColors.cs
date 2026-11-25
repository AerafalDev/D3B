using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>The palette's colours that the studio's drawing code paints with, the same as `Themes/Colors.axaml`'s keys (D560).</summary>
/// <remarks>
/// The maps, the Sequencer and the kinds' icons draw in code, where a resource is not at hand: they read these, which a test
/// keeps equal to the keys the views read, so that a state or a kind has one colour in the whole studio.
/// </remarks>
internal static class StudioColors
{
    /// <summary>Gets the one blue accent, <c>AccentColor</c>.</summary>
    public static Color Accent { get; } = Color.Parse("#0078D4");

    /// <summary>Gets the green of what plays, <c>PlayingColor</c>.</summary>
    public static Color Playing { get; } = Color.Parse("#3FB950");

    /// <summary>Gets the amber of a warning, <c>WarningColor</c>.</summary>
    public static Color Warning { get; } = Color.Parse("#D29922");

    /// <summary>Gets the red of an error, <c>ErrorColor</c>.</summary>
    public static Color Error { get; } = Color.Parse("#F85149");

    /// <summary>Gets a route's teal, <c>RouteColor</c>.</summary>
    public static Color Route { get; } = Color.Parse("#5FD3C0");

    /// <summary>Gets a zone's green, <c>ZoneColor</c>.</summary>
    public static Color Zone { get; } = Color.Parse("#A3D47B");

    /// <summary>Gets a behavior's blue, <c>BehaviorColor</c>.</summary>
    public static Color Behavior { get; } = Color.Parse("#7FA7FF");

    /// <summary>Gets a profile's mauve, <c>ProfileTintColor</c>.</summary>
    public static Color Profile { get; } = Color.Parse("#C9A7F2");
}
