using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Assets;
using D3B.Bot.Assets;

namespace D3B.App.Views;

/// <summary>The assets' panel: Add, the search, the kinds shown, and the project's tree (D326, D358).</summary>
/// <remarks>
/// <para>
/// A click chooses a row, Ctrl and Shift several; a double click or Enter opens it; the arrows move, unfold and fold; F2
/// renames in place, Delete deletes, Ctrl D duplicates, Ctrl X and Ctrl V move, Ctrl Shift C copies the path. A drag takes
/// the rows chosen to a folder, or to the root on the tree's empty space; files dropped from the system's file manager are
/// imported where they fall.
/// </para>
/// <para>
/// The menus are made as they open, as the characters' panel's: what they offer follows the rows, which a binding inside a
/// popup would not reach.
/// </para>
/// </remarks>
internal sealed partial class AssetsView : UserControl
{
    private const double DragStart = 4;

    private static readonly DataFormat<string[]> _rows = DataFormat.CreateInProcessFormat<string[]>("d3b.assets.rows");

    private PointerPressedEventArgs? _pressed;
    private AssetRowViewModel? _pressedRow;
    private bool _choosesOnRelease;
    private bool _dragging;

    /// <summary>Initializes a new instance of the <see cref="AssetsView"/> class.</summary>
    public AssetsView()
    {
        InitializeComponent();
        Tree.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Tree.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        Tree.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private AssetsViewModel? Assets => DataContext as AssetsViewModel;

    private static DrawingGroup? Drawing(string key)
    {
        return DrawingOf.Instance.Convert(key, typeof(DrawingGroup), null, System.Globalization.CultureInfo.InvariantCulture) as DrawingGroup;
    }

    private static Icon IconOf(string key, IBrush? tint = null)
    {
        return new Icon { Drawing = Drawing(key), Width = 16, Height = 16, Tint = tint };
    }

    private static MenuItem Item(string text, string? keys, string? icon, Action run, bool enabled = true, bool danger = false)
    {
        var red = danger ? new SolidColorBrush(Color.Parse("#F3A19A")) : null;
        var item = new MenuItem
        {
            Header = text,
            InputGesture = keys is null ? null : KeyGesture.Parse(keys),
            IsEnabled = enabled,
            Icon = icon is null ? null : IconOf(icon, red),
            Command = new RelayCommand(run),
        };

        if (red is not null)
            item.Foreground = red;

        return item;
    }

    private static Border KindIcon(AssetKindChoice kind)
    {
        return new Border
        {
            Background = kind.KindTint,
            CornerRadius = new CornerRadius(4),
            Width = 16,
            Height = 16,
            Child = new Icon { Drawing = Drawing(kind.Icon), Width = 11, Height = 11, Tint = kind.KindBrush },
        };
    }

    private static AssetRowViewModel? RowOf(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Border { DataContext: AssetRowViewModel row } border && border.Classes.Contains("AssetRow"))
                return row;
        }

        return null;
    }

    private static bool IsOnChevron(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Control { Name: "Chevron" })
                return true;

            if (visual is Border border && border.Classes.Contains("AssetRow"))
                return false;
        }

        return false;
    }

    private static MenuItem NewMenu(AssetsViewModel assets)
    {
        var texts = Localizer.Instance;
        var item = new MenuItem { Header = texts["MenuNew"], Icon = IconOf("General.Add") };

        if (assets.Kinds.FirstOrDefault(kind => kind.Kind is AssetKind.Profile) is { } profile)
            item.Items.Add(new MenuItem { Header = profile.Name, Icon = KindIcon(profile), Command = new RelayCommand(() => assets.New(AssetKind.Profile)) });

        item.Items.Add(new MenuItem { Header = texts["Folder"], Icon = IconOf("Actions.NewFolder"), Command = new RelayCommand(assets.NewFolder) });

        return item;
    }

    private static MenuItem MoveMenu(AssetsViewModel assets, IReadOnlyList<string> keys)
    {
        var item = new MenuItem { Header = Localizer.Instance["MoveTo"], Icon = IconOf("Nodes.Folder") };

        foreach (var folder in assets.AllFolderPaths.Prepend("").Where(folder => assets.CanMove(keys, folder)))
        {
            var name = folder.Length == 0 ? Localizer.Instance["ProjectRoot"] : folder.Replace(Path.DirectorySeparatorChar, '/');

            item.Items.Add(new MenuItem { Header = name, Command = new RelayCommand(() => _ = assets.MoveAsync(keys, folder)) });
        }

        item.IsEnabled = item.Items.Count > 0;

        return item;
    }

    /// <summary>Puts the keyboard in the search: Ctrl Space.</summary>
    public void FocusSearch()
    {
        Search.Focus();
        Search.SelectAll();
    }

    /// <summary>Makes the Add menu: each kind, those of a later session disabled, then a folder and an import, in the folder chosen.</summary>
    /// <returns>The menu, or <see langword="null"/> without the assets.</returns>
    internal ContextMenu? AddMenu()
    {
        if (Assets is not { } assets)
            return null;

        var texts = Localizer.Instance;
        var menu = new ContextMenu { Placement = PlacementMode.BottomEdgeAlignedLeft, VerticalOffset = 6 };

        foreach (var kind in assets.Kinds)
        {
            var item = Item(kind.Name, null, null, () => assets.New(kind.Kind), kind.IsEnabled);

            item.Icon = KindIcon(kind);

            if (kind.Session is { } later)
                item.Header = new DockPanel { Children = { new TextBlock { Text = later, Classes = { "Dim" }, Margin = new Thickness(16, 0, 0, 0), [DockPanel.DockProperty] = Dock.Right }, new TextBlock { Text = kind.Name } } };

            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item(texts["Folder"], null, "Actions.NewFolder", assets.NewFolder));
        menu.Items.Add(Item(texts["ImportFiles"], null, "General.Import", () => _ = assets.ImportAsync()));

        return menu;
    }

    /// <summary>Makes a row's menu, as IntelliJ's: icon, label and shortcut, the destructive last.</summary>
    /// <param name="row">The row, chosen with the others or alone.</param>
    /// <returns>The menu, or <see langword="null"/> for a row with none.</returns>
    internal ContextMenu? RowMenu(AssetRowViewModel row)
    {
        if (Assets is not { } assets || row.IsGivenHeading || row.IsRenaming)
            return null;

        var texts = Localizer.Instance;
        var menu = new ContextMenu();
        var many = row.IsSelected && assets.Rows.Count(item => item.IsSelected) > 1;

        if (many)
        {
            var movable = assets.Rows.Where(item => item.IsSelected && item.IsMovable).Select(item => item.Key).ToList();

            menu.Items.Add(Item(texts["Cut"], "Ctrl+X", null, assets.Cut, movable.Count > 0));
            menu.Items.Add(MoveMenu(assets, movable));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(texts["Delete"], "Delete", "General.Delete", () => assets.AskDelete(), movable.Count > 0, danger: true));

            return menu;
        }

        if (row.IsFolder)
        {
            menu.Items.Add(NewMenu(assets));
            menu.Items.Add(Item(texts["ImportFiles"], null, "General.Import", () => _ = assets.ImportAsync()));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(texts["Cut"], "Ctrl+X", null, assets.Cut));
            menu.Items.Add(Item(texts["PasteHere"], "Ctrl+V", null, () => _ = assets.PasteAsync(), assets.HasCut));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(texts["Rename"], "F2", "General.Edit", () => assets.StartRename(row)));
            menu.Items.Add(Item(texts["CopyPath"], "Ctrl+Shift+C", "General.Copy", () => assets.CopyPath(row)));
            menu.Items.Add(Item(texts["ShowInFolder"], null, "General.Locate", () => assets.ShowInFolder(row)));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(texts["Delete"], "Delete", "General.Delete", () => assets.AskDelete(row), danger: true));

            return menu;
        }

        if (row.IsProblem)
        {
            menu.Items.Add(Item(texts["CopyPath"], "Ctrl+Shift+C", "General.Copy", () => assets.CopyPath(row)));
            menu.Items.Add(Item(texts["ShowInFolder"], null, "General.Locate", () => assets.ShowInFolder(row)));

            return menu;
        }

        menu.Items.Add(Item(texts["MenuOpen"], "Enter", "General.Open", () => assets.OpenRow(row), AssetsViewModel.CanOpen(row)));
        menu.Items.Add(Item(texts["FindUsages"], "Alt+F7", "General.Search", () => assets.FindUsages(row)));
        menu.Items.Add(new Separator());

        if (row.IsGiven)
        {
            menu.Items.Add(Item(texts["CopyToProject"], null, "General.Add", () => _ = assets.CopyToProjectAsync(row)));
            menu.Items.Add(Item(texts["ExportFile"], null, "General.Export", () => _ = assets.ExportAsync(row)));

            return menu;
        }

        menu.Items.Add(Item(texts["Rename"], "F2", "General.Edit", () => assets.StartRename(row)));
        menu.Items.Add(Item(texts["Duplicate"], "Ctrl+D", "General.Copy", () => _ = assets.DuplicateAsync(row)));
        menu.Items.Add(Item(texts["Cut"], "Ctrl+X", null, assets.Cut));
        menu.Items.Add(MoveMenu(assets, [row.Key]));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(texts["ExportFile"], null, "General.Export", () => _ = assets.ExportAsync(row)));
        menu.Items.Add(Item(texts["CopyPath"], "Ctrl+Shift+C", null, () => assets.CopyPath(row)));
        menu.Items.Add(Item(texts["ShowInFolder"], null, "General.Locate", () => assets.ShowInFolder(row)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(texts["Delete"], "Delete", "General.Delete", () => assets.AskDelete(row), danger: true));

        return menu;
    }

    /// <summary>Makes the menu of the tree's empty space: what goes to the root, and the tree's own tools.</summary>
    /// <returns>The menu, or <see langword="null"/> without the assets.</returns>
    internal ContextMenu? TreeMenu()
    {
        if (Assets is not { IsAvailable: true } assets)
            return null;

        var texts = Localizer.Instance;
        var menu = new ContextMenu();

        menu.Items.Add(NewMenu(assets));
        menu.Items.Add(Item(texts["ImportFiles"], null, "General.Import", () => _ = assets.ImportAsync()));
        menu.Items.Add(Item(texts["PasteHere"], "Ctrl+V", null, () => _ = assets.PasteAsync(), assets.HasCut));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(texts["ExpandAll"], null, "General.ExpandAll", assets.ExpandAll));
        menu.Items.Add(Item(texts["CollapseAll"], null, "General.CollapseAll", assets.CollapseAll));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(texts["ReloadProject"], null, "General.Refresh", () => _ = assets.ReloadAsync()));
        menu.Items.Add(Item(texts["ShowProject"], null, "General.Locate", () => assets.ShowInFolder(null)));

        return menu;
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        AddMenu()?.Open(AddButton);
    }

    /// <summary>Opens the menu of the kinds shown under a control, the panel's own tool.</summary>
    /// <param name="anchor">The control the menu opens under.</param>
    public void OpenFilter(Control anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        if (Assets is not { } assets)
            return;

        var menu = new ContextMenu { Placement = PlacementMode.BottomEdgeAlignedRight, VerticalOffset = 6 };

        foreach (var kind in assets.Kinds)
        {
            var item = new MenuItem { Header = kind.Name, Icon = KindIcon(kind), ToggleType = MenuItemToggleType.CheckBox, IsChecked = assets.Shows(kind.Kind), StaysOpenOnClick = true };

            item.Click += (_, _) =>
            {
                assets.ToggleKind(kind.Kind);
                item.IsChecked = assets.Shows(kind.Kind);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Localizer.Instance["ShowAllKinds"], null, null, assets.ShowAllKinds, assets.IsFiltered));
        menu.Open(anchor);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (Assets is not { } assets)
            return;

        if (e.Key is Key.Escape && assets.Filter.Length > 0)
        {
            assets.Filter = "";
            e.Handled = true;
        }
        else if (e.Key is Key.Down)
        {
            Tree.Focus();
            assets.MoveFocus(1);
            BringFocusIntoView();
            e.Handled = true;
        }
    }

    private void OnTreePressed(object? sender, PointerPressedEventArgs e)
    {
        // A press on a row is the row's; one on the empty space chooses nothing, so that what is made goes to the root.
        Tree.Focus();

        if (RowOf(e.Source) is null && e.GetCurrentPoint(Tree).Properties.IsLeftButtonPressed)
            Assets?.ClearSelection();
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (Assets is not { } assets || e.Source is TextBox)
            return;

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.Up:
                assets.MoveFocus(-1, shift);
                break;
            case Key.Down:
                assets.MoveFocus(1, shift);
                break;
            case Key.PageUp:
                assets.MoveFocus(-10, shift);
                break;
            case Key.PageDown:
                assets.MoveFocus(10, shift);
                break;
            case Key.Home:
                assets.MoveFocus(-assets.Rows.Count, shift);
                break;
            case Key.End:
                assets.MoveFocus(assets.Rows.Count, shift);
                break;
            case Key.Left:
                assets.Collapse();
                break;
            case Key.Right:
                assets.Expand();
                break;
            case Key.Enter:
                assets.OpenFocused();
                break;
            case Key.F2:
                assets.StartRename();
                break;
            case Key.Delete:
                assets.AskDelete();
                break;
            case Key.Escape:
                assets.ClearSelection();
                break;
            case Key.D when control:
                _ = assets.DuplicateAsync();
                break;
            case Key.X when control:
                assets.Cut();
                break;
            case Key.V when control:
                _ = assets.PasteAsync();
                break;
            case Key.C when control && shift && assets.Rows.FirstOrDefault(row => row.IsFocused) is { } focused:
                assets.CopyPath(focused);
                break;
            default:
                return;
        }

        BringFocusIntoView();
        e.Handled = true;
    }

    private void BringFocusIntoView()
    {
        Dispatcher.UIThread.Post(() => Tree.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.DataContext is AssetRowViewModel { IsFocused: true } && border.Classes.Contains("AssetRow"))?.BringIntoView());
    }

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: AssetRowViewModel row } || Assets is not { } assets || row.IsRenaming)
            return;

        var point = e.GetCurrentPoint(this);

        e.Handled = true;
        Tree.Focus();

        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (row.Folds && IsOnChevron(e.Source))
        {
            assets.Toggle(row);

            return;
        }

        // The second press of a double click opens the row. The click count is the platform's, whatever lies under the pointer: a
        // click rebuilds the rows, so that the second press lands on a new control, which Avalonia's DoubleTapped, asked of the
        // same control twice, never raised.
        if (e.ClickCount % 2 == 0 && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _pressed = null;
            _pressedRow = null;
            _choosesOnRelease = false;
            assets.OpenRow(row);

            return;
        }

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        _pressed = e;
        _pressedRow = row;

        // A press on a row already chosen among others waits for the release: it may start a drag of them all.
        _choosesOnRelease = row.IsSelected && !control && !shift;

        if (!_choosesOnRelease && e.ClickCount < 2)
            assets.Click(row, control, shift);
    }

    private async void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is not { } pressed || _pressedRow is not { } row || _dragging || Assets is not { } assets || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var from = pressed.GetPosition(this);
        var now = e.GetPosition(this);

        if (Math.Abs(now.X - from.X) < DragStart && Math.Abs(now.Y - from.Y) < DragStart)
            return;

        var keys = assets.DragKeys(row);

        if (keys.Count == 0)
            return;

        _dragging = true;
        _choosesOnRelease = false;

        try
        {
            var data = new DataTransfer();

            data.Add(DataTransferItem.Create(_rows, keys.ToArray()));
            await DragDrop.DoDragDropAsync(pressed, data, DragDropEffects.Move);
        }
        finally
        {
            _dragging = false;
            _pressed = null;
            _pressedRow = null;
            assets.MarkDrop(null);
        }
    }

    private void OnRowReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_choosesOnRelease && _pressedRow is { } row && Assets is { } assets)
            assets.Click(row);

        _choosesOnRelease = false;
        _pressed = null;
        _pressedRow = null;
    }

    // The folder a drop goes into: a folder's own, the folder an asset of the project is in, the root on the empty space.
    private static string? DropFolder(object? source)
    {
        return RowOf(source) switch
        {
            null => "",
            { IsFolder: true, IsNewFolder: false } folder => folder.Folder,
            { IsGiven: false, IsGivenHeading: false } row => row.Folder,
            _ => null,
        };
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (Assets is not { } assets)
            return;

        var folder = DropFolder(e.Source);
        var keys = e.DataTransfer.TryGetValue(_rows);
        var files = e.DataTransfer.Contains(DataFormat.File);

        e.DragEffects = folder is null ? DragDropEffects.None
            : keys is not null ? (assets.CanMove(keys, folder) ? DragDropEffects.Move : DragDropEffects.None)
            : files ? DragDropEffects.Copy
            : DragDropEffects.None;

        assets.MarkDrop(e.DragEffects is DragDropEffects.None || folder is null ? null : assets.Rows.FirstOrDefault(row => row.IsFolder && string.Equals(row.Folder, folder, StringComparison.OrdinalIgnoreCase)));
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        Assets?.MarkDrop(null);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Assets is not { } assets)
            return;

        var folder = DropFolder(e.Source);

        assets.MarkDrop(null);
        e.Handled = true;

        if (folder is null)
            return;

        if (e.DataTransfer.TryGetValue(_rows) is { } keys)
            await assets.MoveAsync(keys, folder);
        else if (e.DataTransfer.TryGetFiles() is { Length: > 0 } items)
            await assets.ImportFilesAsync([.. items.Select(item => item.TryGetLocalPath()).OfType<string>()], folder);
    }

    private void OnRenameAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        void FocusName()
        {
            Dispatcher.UIThread.Post(() =>
            {
                box.Focus();
                box.SelectAll();
            });
        }

        if (box.IsVisible)
            FocusName();

        box.PropertyChanged += (_, change) =>
        {
            if (change.Property == IsVisibleProperty && box.IsVisible)
                FocusName();
        };
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: AssetRowViewModel row } || Assets is not { } assets)
            return;

        if (e.Key is Key.Enter)
        {
            _ = CommitAsync(assets, row);
            e.Handled = true;
        }
        else if (e.Key is Key.Escape)
        {
            assets.CancelRename(row);
            Tree.Focus();
            e.Handled = true;
        }
    }

    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: AssetRowViewModel { IsRenaming: true } row } && Assets is { } assets)
            _ = CommitAsync(assets, row);
    }

    // Once named, the keyboard goes back to the tree, never left in a box that is gone.
    private async Task CommitAsync(AssetsViewModel assets, AssetRowViewModel row)
    {
        await assets.CommitRenameAsync(row);
        Tree.Focus();
    }

    private void OnRowContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: AssetRowViewModel row } target || Assets is not { } assets)
            return;

        e.Handled = true;

        if (!row.IsSelected && !row.IsGivenHeading)
            assets.Click(row);

        // The row was made again as it was chosen: its menu is the one of the row now in its place.
        var current = assets.Rows.FirstOrDefault(item => item.Key == row.Key) ?? row;

        RowMenu(current)?.Open(target);
    }

    private void OnTreeContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled)
            return;

        e.Handled = true;
        Assets?.ClearSelection();
        TreeMenu()?.Open(Tree);
    }
}
