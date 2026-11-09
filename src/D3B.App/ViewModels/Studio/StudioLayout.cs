using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Hosting;

namespace D3B.App.ViewModels.Studio;

/// <summary>The studio's docks: every edge resized on its own, each panel closed by its × and opened again, remembered in <c>app.json</c> (D321).</summary>
/// <remarks>
/// <para>
/// On the left, the characters over the assets, 60 % and 40 % of the column by default, kept as a share so that a resized
/// window keeps it; in the centre, the editor over the bottom panel; on the right, the inspector. A closed panel's
/// neighbour takes its room: the characters or the assets the whole column, the editor the whole centre, and the editor
/// the room of a column with nothing left in it.
/// </para>
/// <para>
/// Each edge stays within bounds as it is dragged, the mockup's: the left column 220 pixels to 40 % of the window, the
/// inspector 240 to 45 %, each of the left column's panels 120 high at least, the bottom panel 90 high and the editor 160
/// above it. A drag's end is remembered, not each step of it.
/// </para>
/// </remarks>
internal sealed partial class StudioLayout : ObservableObject
{
    /// <summary>The left column's width by default.</summary>
    public const double DefaultLeftWidth = 300;

    /// <summary>The inspector's width by default.</summary>
    public const double DefaultRightWidth = 340;

    /// <summary>The characters' share of the left column by default.</summary>
    public const double DefaultLeftShare = 0.6;

    /// <summary>The bottom panel's height by default.</summary>
    public const double DefaultBottomHeight = 260;

    /// <summary>The left column's narrowest.</summary>
    public const double LeastLeftWidth = 220;

    /// <summary>The inspector's narrowest.</summary>
    public const double LeastRightWidth = 240;

    /// <summary>The least height of each of the left column's panels.</summary>
    public const double LeastPanelHeight = 120;

    /// <summary>The bottom panel's lowest.</summary>
    public const double LeastBottomHeight = 90;

    /// <summary>The least height the editor keeps above the bottom panel.</summary>
    public const double LeastEditorHeight = 160;

    private readonly AppPreferences? _preferences;

    /// <summary>Gets the left column's width, in pixels, while it shows.</summary>
    [ObservableProperty]
    public partial double LeftWidth { get; private set; } = DefaultLeftWidth;

    /// <summary>Gets the inspector's width, in pixels, while it shows.</summary>
    [ObservableProperty]
    public partial double RightWidth { get; private set; } = DefaultRightWidth;

    /// <summary>Gets the characters' share of the left column's height while the assets show too.</summary>
    [ObservableProperty]
    public partial double LeftShare { get; private set; } = DefaultLeftShare;

    /// <summary>Gets the bottom panel's height, in pixels, while it shows.</summary>
    [ObservableProperty]
    public partial double BottomHeight { get; private set; } = DefaultBottomHeight;

    /// <summary>Gets whether the characters show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeftOpen), nameof(AreBothLeftOpen))]
    public partial bool IsOutlinerOpen { get; private set; } = true;

    /// <summary>Gets whether the assets show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeftOpen), nameof(AreBothLeftOpen))]
    public partial bool IsAssetsOpen { get; private set; } = true;

    /// <summary>Gets whether the bottom panel shows.</summary>
    [ObservableProperty]
    public partial bool IsBottomOpen { get; private set; } = true;

    /// <summary>Gets whether the inspector shows.</summary>
    [ObservableProperty]
    public partial bool IsDetailsOpen { get; private set; } = true;

    /// <summary>Gets whether the left column shows, one of its panels at least.</summary>
    public bool IsLeftOpen => IsOutlinerOpen || IsAssetsOpen;

    /// <summary>Gets whether both of the left column's panels show, with the edge between them.</summary>
    public bool AreBothLeftOpen => IsOutlinerOpen && IsAssetsOpen;

    /// <summary>Gets whether every edge and panel is as by default.</summary>
    public bool IsDefault => ToState() is null;

    /// <summary>Initializes a new instance of the <see cref="StudioLayout"/> class, as the app was left.</summary>
    /// <param name="preferences">What the app remembers, or <see langword="null"/> to start from the default and remember nothing.</param>
    public StudioLayout(AppPreferences? preferences = null)
    {
        _preferences = preferences;

        if (preferences?.Layout is { } state)
            Apply(state);
    }

    private static double Within(double value, double least, double most)
    {
        // A window too small for both bounds keeps the least, which the window's own least size leaves room for.
        return double.IsFinite(value) ? Math.Max(least, Math.Min(value, Math.Max(least, most))) : least;
    }

    /// <summary>Gets whether a panel shows.</summary>
    /// <param name="panel">The panel.</param>
    /// <returns>Whether it shows.</returns>
    public bool IsOpen(StudioPanel panel)
    {
        return panel switch
        {
            StudioPanel.Outliner => IsOutlinerOpen,
            StudioPanel.Assets => IsAssetsOpen,
            StudioPanel.Bottom => IsBottomOpen,
            _ => IsDetailsOpen,
        };
    }

    /// <summary>Shows or closes a panel, and remembers it.</summary>
    /// <param name="panel">The panel.</param>
    /// <param name="open">Whether it shows.</param>
    public void Show(StudioPanel panel, bool open)
    {
        switch (panel)
        {
            case StudioPanel.Outliner:
                IsOutlinerOpen = open;
                break;
            case StudioPanel.Assets:
                IsAssetsOpen = open;
                break;
            case StudioPanel.Bottom:
                IsBottomOpen = open;
                break;
            default:
                IsDetailsOpen = open;
                break;
        }

        Remember();
    }

    /// <summary>Sets the left column's width as an edge is dragged, within its bounds.</summary>
    /// <param name="width">The width asked.</param>
    /// <param name="windowWidth">The window's width.</param>
    public void ResizeLeft(double width, double windowWidth)
    {
        LeftWidth = Within(width, LeastLeftWidth, windowWidth * 0.4);
    }

    /// <summary>Sets the inspector's width as an edge is dragged, within its bounds.</summary>
    /// <param name="width">The width asked.</param>
    /// <param name="windowWidth">The window's width.</param>
    public void ResizeRight(double width, double windowWidth)
    {
        RightWidth = Within(width, LeastRightWidth, windowWidth * 0.45);
    }

    /// <summary>Sets the characters' share of the left column as the edge between them and the assets is dragged.</summary>
    /// <param name="share">The share asked.</param>
    /// <param name="columnHeight">The height the two panels share.</param>
    public void ResizeShare(double share, double columnHeight)
    {
        var least = columnHeight > 0 ? Math.Min(0.5, LeastPanelHeight / columnHeight) : 0.5;

        LeftShare = Within(share, least, 1 - least);
    }

    /// <summary>Sets the bottom panel's height as its edge is dragged, within its bounds.</summary>
    /// <param name="height">The height asked.</param>
    /// <param name="centreHeight">The height the editor and the bottom panel share.</param>
    public void ResizeBottom(double height, double centreHeight)
    {
        BottomHeight = Within(height, LeastBottomHeight, centreHeight - LeastEditorHeight);
    }

    /// <summary>Remembers the edges where a drag left them.</summary>
    public void Remember()
    {
        _preferences?.RememberLayout(ToState());
        OnPropertyChanged(nameof(IsDefault));
    }

    /// <summary>Gives every edge and panel its default back, and forgets the layout.</summary>
    [RelayCommand]
    public void Reset()
    {
        Apply(new StudioLayoutState(DefaultLeftWidth, DefaultRightWidth, DefaultLeftShare, DefaultBottomHeight, []));
        Remember();
    }

    /// <summary>Shows a closed panel, or closes an open one.</summary>
    /// <param name="panel">The panel.</param>
    [RelayCommand]
    public void Toggle(StudioPanel panel)
    {
        Show(panel, !IsOpen(panel));
    }

    /// <summary>Closes a panel, as its × does.</summary>
    /// <param name="panel">The panel.</param>
    [RelayCommand]
    public void Close(StudioPanel panel)
    {
        Show(panel, false);
    }

    private void Apply(StudioLayoutState state)
    {
        LeftWidth = double.IsFinite(state.LeftWidth) ? Math.Max(LeastLeftWidth, state.LeftWidth) : DefaultLeftWidth;
        RightWidth = double.IsFinite(state.RightWidth) ? Math.Max(LeastRightWidth, state.RightWidth) : DefaultRightWidth;
        LeftShare = double.IsFinite(state.LeftShare) ? Math.Clamp(state.LeftShare, 0.1, 0.9) : DefaultLeftShare;
        BottomHeight = double.IsFinite(state.BottomHeight) ? Math.Max(LeastBottomHeight, state.BottomHeight) : DefaultBottomHeight;

        var closed = state.Closed ?? [];

        IsOutlinerOpen = !closed.Contains(nameof(StudioPanel.Outliner));
        IsAssetsOpen = !closed.Contains(nameof(StudioPanel.Assets));
        IsBottomOpen = !closed.Contains(nameof(StudioPanel.Bottom));
        IsDetailsOpen = !closed.Contains(nameof(StudioPanel.Details));
    }

    private StudioLayoutState? ToState()
    {
        var closed = Enum.GetValues<StudioPanel>().Where(panel => !IsOpen(panel)).Select(panel => panel.ToString()).ToList();
        var state = new StudioLayoutState(Math.Round(LeftWidth), Math.Round(RightWidth), Math.Round(LeftShare, 3), Math.Round(BottomHeight), closed);

        // The default is not written, so that a later default reaches a user who never moved an edge.
        return state is { LeftWidth: DefaultLeftWidth, RightWidth: DefaultRightWidth, LeftShare: DefaultLeftShare, BottomHeight: DefaultBottomHeight, Closed.Count: 0 } ? null : state;
    }
}
