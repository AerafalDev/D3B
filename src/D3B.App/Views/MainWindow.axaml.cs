using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using D3B.App.Controls;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Reports;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Settings;
using ShadUI;

namespace D3B.App.Views;

/// <summary>The studio's window: its title row, its activity bar, its parts laid out as the layout says, its dialogs and toasts (D320 to D322, D513).</summary>
internal sealed partial class MainWindow : ShadUI.Window
{
    // The gap between two docks, which their edges sit in.
    private const double Gap = 5;

    private static readonly DialogKind[] _kinds =
    [
        DialogKind.Of<QuestionView, QuestionViewModel>(440),
        DialogKind.Of<GamePickerView, GamePickerViewModel>(720, 560),
        DialogKind.Of<CellPickerView, CellPickerViewModel>(800, 760),
        DialogKind.Of<NpcPickerView, NpcPickerViewModel>(480, 420),
        DialogKind.Of<RoutePickerView, RoutePickerViewModel>(480, 420),
        DialogKind.Of<RepliesPickerView, RepliesPickerViewModel>(560, 480),
        DialogKind.Of<RouteActionPickerView, RouteActionPickerViewModel>(480, 420),
        DialogKind.Of<NewProfileView, NewProfileViewModel>(440),
        DialogKind.Of<ReportDialogView, ReportDialogViewModel>(640, 520),
    ];

    private readonly DialogManager _dialogManager = new();
    private readonly ToastManager _toastManager = new();
    private MainViewModel? _main;
    private object? _dialog;
    private double _dragStart;
    private bool? _sideShown;
    private bool? _detailsShown;
    private bool? _bottomShown;
    private readonly Dictionary<Control, int> _slides = [];

    /// <summary>Gets the view models the window shows as dialogs.</summary>
    internal static IReadOnlyList<Type> DialogViewModels => [.. _kinds.Select(kind => kind.ViewModel)];

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();

        foreach (var kind in _kinds)
            kind.Register(_dialogManager);

        DialogHost.Manager = _dialogManager;
        ToastHost.Manager = _toastManager;

        LeftSplitter.DragStarted += (_, _) => _dragStart = _main?.Layout.LeftWidth ?? 0;
        LeftSplitter.DragDelta += (_, e) => _main?.Layout.ResizeLeft(_dragStart + e.Vector.X, Bounds.Width);
        RightSplitter.DragStarted += (_, _) => _dragStart = _main?.Layout.RightWidth ?? 0;
        RightSplitter.DragDelta += (_, e) => _main?.Layout.ResizeRight(_dragStart - e.Vector.X, Bounds.Width);
        BottomSplitter.DragStarted += (_, _) => _dragStart = _main?.Layout.BottomHeight ?? 0;
        BottomSplitter.DragDelta += (_, e) => _main?.Layout.ResizeBottom(_dragStart - e.Vector.Y, CentreColumn.Bounds.Height - Gap);

        foreach (var splitter in new[] { LeftSplitter, RightSplitter, BottomSplitter })
            splitter.DragCompleted += (_, _) => _main?.Layout.Remember();

    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_main is not null)
        {
            _main.Layout.PropertyChanged -= OnLayoutChanged;
            _main.Dialogs.PropertyChanged -= OnDialogsChanged;
            _main.Toasts.Shown -= OnToastShown;
            _main.OpenFolder = null;
            _main.RevealFile = null;
            _main.OpenFile = null;
            _main.CopyText = null;
            _main.FocusAssets = null;
            _main.Assets.PickExport = null;
            _main.Assets.PickImport = null;
        }

        _main = DataContext as MainViewModel;

        if (_main is not null)
        {
            _main.Layout.PropertyChanged += OnLayoutChanged;
            _main.Dialogs.PropertyChanged += OnDialogsChanged;
            _main.Toasts.Shown += OnToastShown;
            _main.OpenFolder = OpenFolder;
            _main.RevealFile = RevealFile;
            _main.OpenFile = OpenFile;
            _main.CopyText = text => _ = Clipboard?.SetTextAsync(text);
            _main.FocusAssets = () => Avalonia.Threading.Dispatcher.UIThread.Post(AssetsPanel.FocusSearch);
            _main.Assets.PickExport = PickExportAsync;
            _main.Assets.PickImport = PickImportAsync;
            ApplyLayout();
        }
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        WindowCorners.Round(this);
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _dialogManager.Dispose();
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (_main?.Layout is not { } layout)
            return;

        // A closed panel's row or column takes no room, and its gap goes with it: its neighbour takes the room. Shown or hidden,
        // a part slides as next's (D545); dragged, it follows the pointer.
        var columns = Docks.ColumnDefinitions;
        var centre = CentreColumn.RowDefinitions;

        columns[1].Width = new GridLength(layout.IsSideOpen ? Gap : 0);
        columns[3].Width = new GridLength(layout.IsDetailsOpen ? Gap : 0);
        centre[1].Height = new GridLength(layout.IsBottomOpen ? Gap : 0);
        Place(SideDock, ref _sideShown, layout.IsSideOpen, layout.LeftWidth, columns[0].Width.Value, size => columns[0].Width = new GridLength(size), DockEdge.Left);
        Place(DetailsDock, ref _detailsShown, layout.IsDetailsOpen, layout.RightWidth, columns[4].Width.Value, size => columns[4].Width = new GridLength(size), DockEdge.Right);
        Place(BottomDock, ref _bottomShown, layout.IsBottomOpen, layout.BottomHeight, centre[2].Height.Value, size => centre[2].Height = new GridLength(size), DockEdge.Bottom);
    }

    // A part shown or hidden since the last layout slides to its size or to nothing, its content showing until it is closed; else it
    // takes its size at once, as a drag asks, unless it is sliding.
    private void Place(Border dock, ref bool? shown, bool open, double size, double current, Action<double> resize, DockEdge edge)
    {
        var slides = shown is { } was && was != open;

        shown = open;

        if (!slides)
        {
            if (!_slides.ContainsKey(dock))
            {
                resize(open ? size : 0);
                dock.IsVisible = open;
            }

            return;
        }

        var slide = _slides.GetValueOrDefault(dock) + 1;
        var to = open ? size : 0;
        TimeSpan? began = null;

        _slides[dock] = slide;
        dock.IsVisible = true;

        // The content keeps its size and is uncovered or covered by the dock's edge, as VS Code's: laid out again at each step, a
        // view that adapts to its width would move under the slide.
        Hold(dock.Child, edge, size);

        void Frame(TimeSpan now)
        {
            // A later toggle took over this part.
            if (_slides.GetValueOrDefault(dock) != slide)
                return;

            began ??= now;

            var elapsed = now - began.Value;

            resize(DockSlide.At(current, to, elapsed));

            if (elapsed < DockSlide.Duration)
            {
                RequestAnimationFrame(Frame);

                return;
            }

            _slides.Remove(dock);
            Hold(dock.Child, edge, null);
            dock.IsVisible = open;
        }

        RequestAnimationFrame(Frame);
    }

    // Fixes a dock's content at its size against the edge it slides from, or lets it fill the dock again.
    private static void Hold(Control? content, DockEdge edge, double? size)
    {
        if (content is null)
            return;

        if (edge is DockEdge.Bottom)
        {
            content.Height = size ?? double.NaN;
            content.VerticalAlignment = size is null ? Avalonia.Layout.VerticalAlignment.Stretch : Avalonia.Layout.VerticalAlignment.Top;

            return;
        }

        content.Width = size ?? double.NaN;
        content.HorizontalAlignment = size is null ? Avalonia.Layout.HorizontalAlignment.Stretch
            : edge is DockEdge.Left ? Avalonia.Layout.HorizontalAlignment.Left
            : Avalonia.Layout.HorizontalAlignment.Right;
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        _main?.Search.Close();
    }

    private async void OpenFolder(string folder)
    {
        // The project's folder may not exist until the first asset is written.
        Directory.CreateDirectory(folder);

        if (Launcher is { } launcher)
            await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder));
    }

    private async void OpenFile(string target)
    {
        if (Launcher is not { } launcher)
            return;

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            await launcher.LaunchUriAsync(uri);
        else if (File.Exists(target))
            await launcher.LaunchFileInfoAsync(new FileInfo(target));
    }

    // Explorer selects the file shown; elsewhere its folder opens.
    private void RevealFile(string file)
    {
        if (OperatingSystem.IsWindows() && File.Exists(file))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = false })?.Dispose();
        else
            OpenFolder(Directory.Exists(file) ? file : Path.GetDirectoryName(file) ?? file);
    }

    private async Task<string?> PickExportAsync(string name)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = name, DefaultExtension = ".json" });

        return file?.TryGetLocalPath();
    }

    private async Task<IReadOnlyList<string>> PickImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("D3B") { Patterns = ["*.json"] }],
        });

        return [.. files.Select(file => file.TryGetLocalPath()).OfType<string>()];
    }

    private void OnAssetsFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control anchor)
            AssetsPanel.OpenFilter(anchor);
    }

    private void OnDialogsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModels.Dialogs.Current) || sender is not ViewModels.Dialogs dialogs)
            return;

        // ShadUI finds a dialog's view by its context's own type, so each is shown as what it is.
        if (_dialog is not null)
            _dialogManager.Close(_dialog);

        _dialog = dialogs.Current;

        if (_dialog is null)
            return;

        // Every dialog view model has its row: a test holds the table complete, so a missing one fails there and not in a player's hands.
        _kinds.Single(kind => kind.ViewModel == _dialog.GetType()).Show(_dialogManager, _dialog);
    }

    private void OnToastShown(Toast toast)
    {
        var builder = _toastManager.CreateToast(toast.Text).DismissOnClick().WithDelay(5);

        if (toast is { ActionText: { } text, Action: { } action })
            builder = builder.WithAction(text, action);

        switch (toast.Tone)
        {
            case Tone.Success:
                builder.ShowSuccess();
                break;
            case Tone.Warning:
                builder.ShowWarning();
                break;
            case Tone.Danger:
                builder.ShowError();
                break;
            case Tone.Info:
                builder.ShowInfo();
                break;
            default:
                builder.Show();
                break;
        }
    }
}
