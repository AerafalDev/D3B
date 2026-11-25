using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Hosting;

namespace D3B.App.ViewModels.Studio;

/// <summary>The studio's parts: the activity bar's view in the side bar, the bottom panel and the Details panel, each edge resized on its own, remembered in <c>app.json</c> (D321, D513).</summary>
/// <remarks>
/// <para>
/// As <c>next</c>'s shell and VS Code's: the activity bar chooses the side bar's view, the characters, the library or the
/// world, and choosing the view shown again hides the side bar. The studio opens on the side bar and the editor alone; the
/// bottom panel and the Details panel start hidden and show when asked, or when what they hold is needed. A hidden part's
/// neighbour takes its room. The Details are never remembered: shown by a selection, they would open the next start on
/// nothing selected, so every start leaves them hidden until something is.
/// </para>
/// <para>
/// Each edge stays within bounds as it is dragged: the side bar 220 pixels to 40 % of the window, the Details 240 to 45 %,
/// the bottom panel 90 high and the editor 160 above it. A drag's end is remembered, not each step of it. The advanced
/// tools, the protocol, show only once turned on, which the layout remembers with it and its default keeps.
/// </para>
/// </remarks>
internal sealed partial class StudioLayout : ObservableObject
{
    /// <summary>The side bar's width by default.</summary>
    public const double DefaultLeftWidth = 300;

    /// <summary>The Details panel's width by default.</summary>
    public const double DefaultRightWidth = 340;

    /// <summary>The bottom panel's height by default.</summary>
    public const double DefaultBottomHeight = 260;

    /// <summary>The side bar's narrowest.</summary>
    public const double LeastLeftWidth = 220;

    /// <summary>The Details panel's narrowest.</summary>
    public const double LeastRightWidth = 240;

    /// <summary>The bottom panel's lowest.</summary>
    public const double LeastBottomHeight = 90;

    /// <summary>The least height the editor keeps above the bottom panel.</summary>
    public const double LeastEditorHeight = 160;

    private readonly AppPreferences? _preferences;

    /// <summary>Gets the side bar's width, in pixels, while it shows.</summary>
    [ObservableProperty]
    public partial double LeftWidth { get; private set; } = DefaultLeftWidth;

    /// <summary>Gets the Details panel's width, in pixels, while it shows.</summary>
    [ObservableProperty]
    public partial double RightWidth { get; private set; } = DefaultRightWidth;

    /// <summary>Gets the bottom panel's height, in pixels, while it shows.</summary>
    [ObservableProperty]
    public partial double BottomHeight { get; private set; } = DefaultBottomHeight;

    /// <summary>Gets the side bar's view, which it shows while it is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCharactersShown), nameof(IsLibraryShown), nameof(IsWorldShown))]
    public partial SideView View { get; private set; }

    /// <summary>Gets whether the side bar shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCharactersShown), nameof(IsLibraryShown), nameof(IsWorldShown))]
    public partial bool IsSideOpen { get; private set; } = true;

    /// <summary>Gets whether the bottom panel shows.</summary>
    [ObservableProperty]
    public partial bool IsBottomOpen { get; private set; }

    /// <summary>Gets whether the Details panel shows.</summary>
    [ObservableProperty]
    public partial bool IsDetailsOpen { get; private set; }

    /// <summary>Gets whether the advanced tools show: the protocol's tab of the bottom panel.</summary>
    [ObservableProperty]
    public partial bool AdvancedTools { get; private set; }

    /// <summary>Gets whether the side bar shows the characters, the activity bar's entry pressed.</summary>
    public bool IsCharactersShown => IsSideOpen && View is SideView.Characters;

    /// <summary>Gets whether the side bar shows the library.</summary>
    public bool IsLibraryShown => IsSideOpen && View is SideView.Library;

    /// <summary>Gets whether the side bar shows the world.</summary>
    public bool IsWorldShown => IsSideOpen && View is SideView.World;

    /// <summary>Gets whether every edge and part is as by default.</summary>
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

    /// <summary>Gets whether a part shows.</summary>
    /// <param name="panel">The part.</param>
    /// <returns>Whether it shows.</returns>
    public bool IsOpen(StudioPanel panel)
    {
        return panel switch
        {
            StudioPanel.Side => IsSideOpen,
            StudioPanel.Bottom => IsBottomOpen,
            _ => IsDetailsOpen,
        };
    }

    /// <summary>Shows or hides a part, and remembers it.</summary>
    /// <param name="panel">The part.</param>
    /// <param name="open">Whether it shows.</param>
    public void Show(StudioPanel panel, bool open)
    {
        if (IsOpen(panel) == open)
            return;

        switch (panel)
        {
            case StudioPanel.Side:
                IsSideOpen = open;
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

    /// <summary>Shows a view in the side bar, as an entry of the activity bar does: chosen while it shows, it hides the side bar.</summary>
    /// <param name="view">The view.</param>
    [RelayCommand]
    public void ShowView(SideView view)
    {
        if (IsSideOpen && View == view)
        {
            Show(StudioPanel.Side, false);

            return;
        }

        Reveal(view);
    }

    /// <summary>Shows a view in the side bar whether it shows already or not, as a key or a link asks.</summary>
    /// <param name="view">The view.</param>
    public void Reveal(SideView view)
    {
        if (IsSideOpen && View == view)
            return;

        View = view;
        IsSideOpen = true;
        Remember();
    }

    /// <summary>Shows the advanced tools, or hides them.</summary>
    [RelayCommand]
    public void ToggleAdvancedTools()
    {
        AdvancedTools = !AdvancedTools;
        Remember();
    }

    /// <summary>Sets the side bar's width as an edge is dragged, within its bounds.</summary>
    /// <param name="width">The width asked.</param>
    /// <param name="windowWidth">The window's width.</param>
    public void ResizeLeft(double width, double windowWidth)
    {
        LeftWidth = Within(width, LeastLeftWidth, windowWidth * 0.4);
    }

    /// <summary>Sets the Details panel's width as an edge is dragged, within its bounds.</summary>
    /// <param name="width">The width asked.</param>
    /// <param name="windowWidth">The window's width.</param>
    public void ResizeRight(double width, double windowWidth)
    {
        RightWidth = Within(width, LeastRightWidth, windowWidth * 0.45);
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

    /// <summary>Gives every edge and part its default back, and forgets the layout; the advanced tools stay as they are.</summary>
    [RelayCommand]
    public void Reset()
    {
        Apply(new StudioLayoutState(DefaultLeftWidth, DefaultRightWidth, DefaultBottomHeight, AdvancedTools: AdvancedTools) { Shown = [nameof(StudioPanel.Side)] });
        Remember();
    }

    /// <summary>Shows a hidden part, or hides a shown one.</summary>
    /// <param name="panel">The part.</param>
    [RelayCommand]
    public void Toggle(StudioPanel panel)
    {
        Show(panel, !IsOpen(panel));
    }

    /// <summary>Hides a part, as its × does.</summary>
    /// <param name="panel">The part.</param>
    [RelayCommand]
    public void Close(StudioPanel panel)
    {
        Show(panel, false);
    }

    private void Apply(StudioLayoutState state)
    {
        LeftWidth = double.IsFinite(state.LeftWidth) ? Math.Max(LeastLeftWidth, state.LeftWidth) : DefaultLeftWidth;
        RightWidth = double.IsFinite(state.RightWidth) ? Math.Max(LeastRightWidth, state.RightWidth) : DefaultRightWidth;
        BottomHeight = double.IsFinite(state.BottomHeight) ? Math.Max(LeastBottomHeight, state.BottomHeight) : DefaultBottomHeight;
        View = Enum.TryParse<SideView>(state.View, out var view) && Enum.IsDefined(view) ? view : SideView.Characters;
        AdvancedTools = state.AdvancedTools;

        if (state.Shown is { } shown)
        {
            IsSideOpen = shown.Contains(nameof(StudioPanel.Side));
            IsBottomOpen = shown.Contains(nameof(StudioPanel.Bottom));
            IsDetailsOpen = false;

            return;
        }

        // An older file's four docks: the side bar goes only with both of its left panels, the others take the new defaults.
        var closed = state.Closed ?? [];

        IsSideOpen = !(closed.Contains("Outliner") && closed.Contains("Assets"));
        IsBottomOpen = false;
        IsDetailsOpen = false;
    }

    private StudioLayoutState? ToState()
    {
        var shown = Enum.GetValues<StudioPanel>().Where(panel => panel is not StudioPanel.Details && IsOpen(panel)).Select(panel => panel.ToString()).ToList();
        var state = new StudioLayoutState(Math.Round(LeftWidth), Math.Round(RightWidth), Math.Round(BottomHeight), Shown: shown, View: View.ToString(), AdvancedTools: AdvancedTools);

        // The default is not written, so that a later default reaches a user who never moved an edge.
        return state is { LeftWidth: DefaultLeftWidth, RightWidth: DefaultRightWidth, BottomHeight: DefaultBottomHeight, View: nameof(SideView.Characters), AdvancedTools: false } && shown is [nameof(StudioPanel.Side)] ? null : state;
    }
}
