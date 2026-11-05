using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Routes;

/// <summary>An anchor in the inspector's list of steps: its number, its place, and the way to the next (D299, D371).</summary>
internal sealed partial class RouteStepViewModel : ObservableObject
{
    private readonly Action<int> _select;
    private readonly Action<int> _remove;
    private readonly Action<int> _addAction;

    /// <summary>Gets the step's place in the route, from 0.</summary>
    public int Index { get; }

    /// <summary>Gets the step's number, from 1, as the map's pill shows it.</summary>
    public int Number => Index + 1;

    /// <summary>Gets what a screen reader says of the step: its number and where its map lies.</summary>
    public string Summary => Number.ToString(System.Globalization.CultureInfo.CurrentCulture) + ". " + Place;

    /// <summary>Gets the step's map.</summary>
    public long MapId { get; }

    /// <summary>Gets where the map lies: its coordinates, or that it is an inside at them.</summary>
    public string Place { get; }

    /// <summary>Gets the name of the map's zone, else why it has none.</summary>
    public string Zone { get; }

    /// <summary>Gets whether the step was a map of the path that the user pinned.</summary>
    public bool IsPinned { get; }

    /// <summary>Gets whether the step lies in a world other than the layer shown.</summary>
    public bool IsElsewhere { get; }

    /// <summary>Gets the way to the next step, said in words; empty after the last one unless it leads back to the first.</summary>
    public string Gap { get; }

    /// <summary>Gets whether a way to the next step is said.</summary>
    public bool HasGap => Gap.Length > 0;

    /// <summary>Gets whether no path leads to the next step, which is said in red.</summary>
    public bool IsGapBroken { get; }

    /// <summary>Gets whether the way back to the first step is meant: a loop's closing leg, said fainter.</summary>
    public bool IsGapBack { get; }

    /// <summary>Gets how many actions the route plays on the step's map (D377).</summary>
    public int ActionCount { get; }

    /// <summary>Gets the count in words, such as "2 actions"; empty when there is none.</summary>
    public string ActionsText { get; }

    /// <summary>Gets whether the step has actions, which its row counts.</summary>
    public bool HasActions => ActionCount > 0;

    /// <summary>Gets whether the step is the one chosen.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Initializes a new instance of the <see cref="RouteStepViewModel"/> class.</summary>
    /// <param name="index">The step's place in the route.</param>
    /// <param name="mapId">The step's map.</param>
    /// <param name="place">Where the map lies.</param>
    /// <param name="zone">The name of the map's zone.</param>
    /// <param name="isPinned">Whether the user pinned the step.</param>
    /// <param name="isElsewhere">Whether the step lies in another world than the layer shown.</param>
    /// <param name="gap">The way to the next step, in words.</param>
    /// <param name="isGapBroken">Whether no path leads to the next step.</param>
    /// <param name="isGapBack">Whether the way is a loop's way back to the first step.</param>
    /// <param name="actionCount">How many actions the step has.</param>
    /// <param name="actionsText">The count in words.</param>
    /// <param name="select">What choosing the step does, given its place.</param>
    /// <param name="remove">What removing the step does, given its place.</param>
    /// <param name="addAction">What the button that adds an action does, given the step's place.</param>
    public RouteStepViewModel(int index, long mapId, string place, string zone, bool isPinned, bool isElsewhere, string gap, bool isGapBroken, bool isGapBack, int actionCount, string actionsText, Action<int> select, Action<int> remove, Action<int> addAction)
    {
        Index = index;
        MapId = mapId;
        Place = place;
        Zone = zone;
        IsPinned = isPinned;
        IsElsewhere = isElsewhere;
        Gap = gap;
        IsGapBroken = isGapBroken;
        IsGapBack = isGapBack;
        ActionCount = actionCount;
        ActionsText = actionsText;
        _select = select;
        _remove = remove;
        _addAction = addAction;
    }

    [RelayCommand]
    private void Select()
    {
        _select(Index);
    }

    [RelayCommand]
    private void AddAction()
    {
        _addAction(Index);
    }

    [RelayCommand]
    private void Remove()
    {
        _remove(Index);
    }
}
