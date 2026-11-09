using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Studio.Assets;

namespace D3B.App.ViewModels.Assets;

/// <summary>A row of the assets' tree: a folder, an asset, a file that does not read, or the given assets' heading (D326).</summary>
internal sealed partial class AssetRowViewModel : ObservableObject
{
    private const double Step = 16;

    /// <summary>Gets what the row is.</summary>
    public AssetRowKind Kind { get; }

    /// <summary>Gets the row's name: the asset's, in the app's language for a given one, or the folder's.</summary>
    public string Name { get; }

    /// <summary>Gets how deep the row is, 0 for the project's own root.</summary>
    public int Depth { get; }

    /// <summary>Gets the row's indent, in pixels.</summary>
    public double Indent => Depth * Step;

    /// <summary>Gets what keys the row from one tree to the next: an asset's id, <c>folder:</c> and a folder's path, or <c>problem:</c> and a file's.</summary>
    public string Key { get; init; } = "";

    /// <summary>Gets what the row's tooltip says: its kind and file, or its folder's path, or why it does not read.</summary>
    public string? Tip { get; init; }

    /// <summary>Gets whether the keyboard is on the row.</summary>
    public bool IsFocused { get; init; }

    /// <summary>Gets whether the row is cut, waiting to be pasted, shown dim.</summary>
    public bool IsCut { get; init; }

    /// <summary>Gets the asset's id, for an asset's row.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the asset's kind, for an asset's row.</summary>
    public AssetKind? AssetKind { get; init; }

    /// <summary>Gets the folder, relative to the project's, for a folder's row; the folder an asset or a problem is in, for theirs.</summary>
    public string Folder { get; init; } = "";

    /// <summary>Gets whether the asset was given with D3B, read only.</summary>
    public bool IsGiven { get; init; }

    /// <summary>Gets how many use the asset, or how many assets a folder holds, if any.</summary>
    public string Count { get; init; } = "";

    /// <summary>Gets what the count says, in its tooltip.</summary>
    public string? CountTip { get; init; }

    /// <summary>Gets why a file does not read, in its tooltip.</summary>
    public string? Problem { get; init; }

    /// <summary>Gets whether the asset has changes not saved, which a dot says.</summary>
    public bool IsUnsaved { get; init; }

    /// <summary>Gets whether a folder or the given heading is unfolded.</summary>
    public bool IsExpanded { get; init; }

    /// <summary>Gets the name before the part the search found.</summary>
    public string Before { get; init; } = "";

    /// <summary>Gets the part of the name the search found, lit.</summary>
    public string Found { get; init; } = "";

    /// <summary>Gets the name after the part the search found.</summary>
    public string After { get; init; } = "";

    /// <summary>Gets whether the row is a folder not made yet, its name being typed.</summary>
    public bool IsNewFolder { get; init; }

    /// <summary>Gets whether the row may be chosen: an asset, a folder or a file that does not read.</summary>
    public bool IsSelectable => Kind is not AssetRowKind.Given && !IsNewFolder;

    /// <summary>Gets whether the row may be moved, renamed or deleted: the project's own asset or folder.</summary>
    public bool IsMovable => Kind is AssetRowKind.Asset or AssetRowKind.Folder && !IsGiven && !IsNewFolder;

    /// <summary>Gets whether the row is an asset's.</summary>
    public bool IsAsset => Kind is AssetRowKind.Asset;

    /// <summary>Gets whether the row is a folder's.</summary>
    public bool IsFolder => Kind is AssetRowKind.Folder;

    /// <summary>Gets whether the row is a file that does not read.</summary>
    public bool IsProblem => Kind is AssetRowKind.Problem;

    /// <summary>Gets whether the row is the given assets' heading.</summary>
    public bool IsGivenHeading => Kind is AssetRowKind.Given;

    /// <summary>Gets whether the row folds, a folder or the given heading.</summary>
    public bool Folds => Kind is AssetRowKind.Folder or AssetRowKind.Given;

    /// <summary>Gets whether the asset's kind shows its icon.</summary>
    public bool HasKind => AssetKind is not null;

    /// <summary>Gets whether a count shows.</summary>
    public bool HasCount => Count.Length > 0 && !IsProblem;

    /// <summary>Gets the kind's IntelliJ icon, by its key.</summary>
    public string? Icon => AssetKind is { } kind ? AssetKinds.IconOf(kind) : null;

    /// <summary>Gets the kind's colour.</summary>
    public IBrush? KindBrush => AssetKind is { } kind ? new SolidColorBrush(AssetKinds.ColorOf(kind)) : null;

    /// <summary>Gets the kind's dark tint.</summary>
    public IBrush? KindTint => AssetKind is { } kind ? new SolidColorBrush(AssetKinds.TintOf(kind)) : null;

    /// <summary>Gets whether the row is one of those chosen.</summary>
    public bool IsSelected { get; init; }

    /// <summary>Gets or sets whether a drag is over the folder, which then takes what it carries.</summary>
    [ObservableProperty]
    public partial bool IsDropTarget { get; set; }

    /// <summary>Gets or sets whether the row's name is being edited in place.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingName))]
    public partial bool IsRenaming { get; set; }

    /// <summary>Gets or sets the name being typed in place.</summary>
    [ObservableProperty]
    public partial string EditName { get; set; } = "";

    /// <summary>Gets whether the row shows its name, not editing it.</summary>
    public bool IsShowingName => !IsRenaming;

    /// <summary>Initializes a new instance of the <see cref="AssetRowViewModel"/> class.</summary>
    /// <param name="kind">What the row is.</param>
    /// <param name="name">Its name.</param>
    /// <param name="depth">How deep it is.</param>
    public AssetRowViewModel(AssetRowKind kind, string name, int depth)
    {
        Kind = kind;
        Name = name;
        Depth = depth;
        Before = name;
    }
}
