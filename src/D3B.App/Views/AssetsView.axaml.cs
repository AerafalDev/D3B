using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Assets;

namespace D3B.App.Views;

/// <summary>The assets' panel: Add, the search, and the project's tree (D326).</summary>
/// <remarks>
/// A click chooses an asset for the inspector or folds a folder, a double click or Enter opens it, F2 renames it in place,
/// Ctrl D duplicates it, Delete asks to delete it; the right click chooses it, then opens its menu, made as it opens, as the
/// characters' panel does, since what it offers follows the row.
/// </remarks>
internal sealed partial class AssetsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="AssetsView"/> class.</summary>
    public AssetsView()
    {
        InitializeComponent();
    }

    private static MenuItem Item(string text, string? keys, Action run, bool enabled = true)
    {
        var item = new MenuItem { Header = text, InputGesture = keys is null ? null : KeyGesture.Parse(keys), IsEnabled = enabled };

        item.Click += (_, _) => run();

        return item;
    }

    private static DrawingGroup? Drawing(string key)
    {
        return DrawingOf.Instance.Convert(key, typeof(DrawingGroup), null, System.Globalization.CultureInfo.InvariantCulture) as DrawingGroup;
    }

    private static MenuItem Danger(MenuItem item)
    {
        item.Foreground = new SolidColorBrush(Color.Parse("#F3A19A"));

        return item;
    }

    private static Border KindIcon(AssetKindChoice kind)
    {
        return new Border
        {
            Background = kind.KindTint,
            CornerRadius = new Avalonia.CornerRadius(5),
            Width = 18,
            Height = 18,
            Child = new Icon { Drawing = Drawing(kind.Icon), Width = 11, Height = 11, Tint = kind.KindBrush },
        };
    }

    /// <summary>Puts the keyboard in the search: Ctrl Space.</summary>
    public void FocusSearch()
    {
        Search.Focus();
        Search.SelectAll();
    }

    /// <summary>Makes the Add menu: each kind, those of a later session disabled, then a folder and an import.</summary>
    /// <returns>The menu, or <see langword="null"/> without the assets.</returns>
    internal ContextMenu? AddMenu()
    {
        if (DataContext is not AssetsViewModel assets)
            return null;

        var texts = Localizer.Instance;
        var menu = new ContextMenu { Placement = PlacementMode.BottomEdgeAlignedLeft, VerticalOffset = 6 };

        menu.Items.Add(new MenuItem { Header = texts["AddNew"], Classes = { "Heading" }, IsEnabled = false });

        foreach (var kind in assets.Kinds)
        {
            var item = Item(kind.Name, null, () => assets.New(kind.Kind), kind.IsEnabled);

            item.Icon = KindIcon(kind);

            if (kind.Session is { } later)
                item.Header = new DockPanel { Children = { new TextBlock { Text = later, Classes = { "Dim" }, Margin = new Avalonia.Thickness(16, 0, 0, 0), [DockPanel.DockProperty] = Dock.Right }, new TextBlock { Text = kind.Name } } };

            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = texts["Folder"], Icon = new Icon { Drawing = Drawing("Nodes.Folder"), Width = 16, Height = 16 }, Command = assets.NewFolderCommand });
        menu.Items.Add(new MenuItem { Header = texts["ImportFile"], Icon = new Icon { Drawing = Drawing("General.Import"), Width = 16, Height = 16 }, Command = assets.ImportCommand });
        return menu;
    }

    // Made as it opens, as the tree's own menu: a flyout filled while it opens shows empty.
    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        AddMenu()?.Open(AddButton);
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AssetRowViewModel row } control && DataContext is AssetsViewModel assets && !row.IsRenaming)
        {
            assets.Select(row);
            control.Focus();
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AssetRowViewModel row } && DataContext is AssetsViewModel assets)
            assets.OpenRow(row);
    }

    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: AssetRowViewModel row } || DataContext is not AssetsViewModel assets || row.IsRenaming)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                assets.OpenRow(row);
                break;
            case Key.Space:
                assets.Select(row);
                break;
            case Key.F2:
                assets.StartRename(row);
                break;
            case Key.Delete:
                assets.AskDelete(row);
                break;
            case Key.D when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _ = assets.DuplicateAsync(row);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnRenameAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox box)
            box.PropertyChanged += (_, change) =>
            {
                if (change.Property == IsVisibleProperty && box.IsVisible)
                    Dispatcher.UIThread.Post(() => { box.Focus(); box.SelectAll(); });
            };
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: AssetRowViewModel row } || DataContext is not AssetsViewModel assets)
            return;

        if (e.Key is Key.Enter)
        {
            _ = assets.CommitRenameAsync(row);
            e.Handled = true;
        }
        else if (e.Key is Key.Escape)
        {
            assets.CancelRename(row);
            e.Handled = true;
        }
    }

    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: AssetRowViewModel { IsRenaming: true } row } && DataContext is AssetsViewModel assets)
            _ = assets.CommitRenameAsync(row);
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: AssetRowViewModel row } target || DataContext is not AssetsViewModel assets || row.IsGivenHeading || row.IsRenaming)
            return;

        e.Handled = true;

        if (row.IsAsset)
            assets.Select(row);
        else
            assets.ChooseFolder(row);

        var texts = Localizer.Instance;
        var menu = new ContextMenu();

        if (row.IsAsset)
        {
            menu.Items.Add(Item(texts["MenuOpen"], "Enter", () => assets.OpenRow(row), AssetsViewModel.CanOpen(row)));
            menu.Items.Add(Item(texts["FindUsages"], "Alt+F7", () => assets.FindUsages(row)));
            menu.Items.Add(new Separator());

            if (row.IsGiven)
            {
                menu.Items.Add(Item(texts["CopyToProject"], null, () => _ = assets.CopyToProjectAsync(row)));
                menu.Items.Add(Item(texts["ExportFile"], null, () => _ = assets.ExportAsync(row)));
            }
            else
            {
                menu.Items.Add(Item(texts["Rename"], "F2", () => assets.StartRename(row)));
                menu.Items.Add(Item(texts["Duplicate"], "Ctrl+D", () => _ = assets.DuplicateAsync(row)));

                var move = new MenuItem { Header = texts["MoveTo"] };

                foreach (var (folder, name) in assets.FoldersFor(row))
                    move.Items.Add(Item(name, null, () => _ = assets.MoveAsync(row, folder)));

                menu.Items.Add(move);
                menu.Items.Add(Item(texts["ExportFile"], null, () => _ = assets.ExportAsync(row)));
                menu.Items.Add(Item(texts["ShowInFolder"], null, () => assets.ShowInFolder(row)));
                menu.Items.Add(new Separator());
                menu.Items.Add(Danger(Item(texts["Delete"], "Delete", () => assets.AskDelete(row))));
            }
        }
        else if (row.IsFolder)
        {
            menu.Items.Add(Item(texts["NewSubfolder"], null, assets.NewFolder));
            menu.Items.Add(Item(texts["ImportHere"], null, () => _ = assets.ImportAsync()));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(texts["Rename"], "F2", () => assets.StartRename(row)));
            menu.Items.Add(Item(texts["ShowInFolder"], null, () => assets.ShowInFolder(row)));
            menu.Items.Add(new Separator());
            menu.Items.Add(Danger(Item(texts["DeleteFolder"], "Delete", () => assets.AskDelete(row))));
        }
        else
        {
            menu.Items.Add(Item(texts["ShowInFolder"], null, () => assets.ShowInFolder(row)));
        }

        menu.Open(target);
    }
}
