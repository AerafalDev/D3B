using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace D3B.App.ViewModels.World;

/// <summary>A row of the Outliner: a heading, a character, a route, a step of a route or a zone (D397).</summary>
internal sealed partial class OutlinerRowViewModel : ObservableObject
{
    /// <summary>Gets what the row is, the same each time the Outliner is read again: <c>route:</c>, <c>step:</c>, <c>zone:</c>, <c>character:</c> and the id.</summary>
    public string Key { get; }

    /// <summary>Gets what the row is.</summary>
    public OutlinerRowKind Kind { get; }

    /// <summary>Gets the route or the zone's id, the character's key as text, or the heading's name.</summary>
    public string Id { get; init; } = "";

    /// <summary>Gets a step's place in its route, from 0, or -1.</summary>
    public int Step { get; init; } = -1;

    /// <summary>Gets how far the row is indented, in pixels.</summary>
    public double Indent { get; init; }

    /// <summary>Gets the row's name.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "";

    /// <summary>Gets what the row adds in fainter text: a character's activity, a route's steps, a step's zone, a zone's size.</summary>
    [ObservableProperty]
    public partial string Detail { get; set; } = "";

    /// <summary>Gets the count a heading shows on its right.</summary>
    [ObservableProperty]
    public partial string Count { get; set; } = "";

    /// <summary>Gets the row's IntelliJ icon, for a route or a zone.</summary>
    public string? IconKey { get; init; }

    /// <summary>Gets the head of a character, else its initials on its breed's colour.</summary>
    [ObservableProperty]
    public partial IImage? Head { get; set; }

    /// <summary>Gets a character's initials.</summary>
    public string Initials { get; init; } = "";

    /// <summary>Gets a character's colour.</summary>
    [ObservableProperty]
    public partial IBrush? PortraitColor { get; set; }

    /// <summary>Gets the colour of a character's dot, its state's.</summary>
    [ObservableProperty]
    public partial IBrush? StateBrush { get; set; }

    /// <summary>Gets whether the character is offline, its row dimmed.</summary>
    [ObservableProperty]
    public partial bool IsDimmed { get; set; }

    /// <summary>Gets whether the row is what is selected in the world.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets whether the row unfolds.</summary>
    public bool CanExpand { get; init; }

    /// <summary>Gets whether the row is unfolded.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gets whether the row has an eye: the map shows what it names, or leaves it out.</summary>
    public bool HasEye { get; init; }

    /// <summary>Gets whether the eye is open.</summary>
    [ObservableProperty]
    public partial bool IsEyeOpen { get; set; } = true;

    /// <summary>Gets whether the route or the zone holds changes not saved.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    /// <summary>Gets the initials of the characters that follow the route, or are at the step, or nothing (D419).</summary>
    [ObservableProperty]
    public partial string Followers { get; set; } = "";

    /// <summary>Gets whether a character that follows is on the step's map, rather than on its way there.</summary>
    [ObservableProperty]
    public partial bool IsArrived { get; set; }

    /// <summary>Gets whether a character follows the route, or is at the step.</summary>
    public bool IsFollowed => Followers.Length > 0;

    /// <summary>Gets the small dots of what a step does.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<IBrush> Dots { get; set; } = [];

    /// <summary>Gets whether the row has dots.</summary>
    public bool HasDots => Dots.Count > 0;

    /// <summary>Gets whether the row is a heading.</summary>
    public bool IsHeading => Kind is OutlinerRowKind.Heading;

    /// <summary>Gets whether the row is a character.</summary>
    public bool IsCharacter => Kind is OutlinerRowKind.Character;

    /// <summary>Gets whether the row is a route or a zone, which have an icon.</summary>
    public bool HasIcon => IconKey is not null;

    /// <summary>Gets whether the row is something that can be selected.</summary>
    public bool IsItem => Kind is not OutlinerRowKind.Heading;

    /// <summary>Initializes a new instance of the <see cref="OutlinerRowViewModel"/> class.</summary>
    /// <param name="key">What the row is.</param>
    /// <param name="kind">What kind of row it is.</param>
    public OutlinerRowViewModel(string key, OutlinerRowKind kind)
    {
        Key = key;
        Kind = kind;
    }

    partial void OnFollowersChanged(string value)
    {
        OnPropertyChanged(nameof(IsFollowed));
    }

    partial void OnDotsChanged(IReadOnlyList<IBrush> value)
    {
        OnPropertyChanged(nameof(HasDots));
    }
}
