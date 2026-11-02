using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using D3B.App.Controls;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using ShadUI;

namespace D3B.App.Views;

/// <summary>The studio's window: its title row, its docks laid out as the layout says, its dialogs and toasts (D320 to D322).</summary>
internal sealed partial class MainWindow : ShadUI.Window
{
    // The gap between two docks, which their edges sit in.
    private const double Gap = 5;

    private readonly DialogManager _dialogManager = new DialogManager()
        .Register<QuestionView, QuestionViewModel>()
        .Register<GamePickerView, GamePickerViewModel>()
        .Register<NewProfileView, NewProfileViewModel>();

    private readonly ToastManager _toastManager = new();
    private MainViewModel? _main;
    private object? _dialog;
    private double _dragStart;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DialogHost.Manager = _dialogManager;
        ToastHost.Manager = _toastManager;

        LeftSplitter.DragStarted += (_, _) => _dragStart = _main?.Layout.LeftWidth ?? 0;
        LeftSplitter.DragDelta += (_, e) => _main?.Layout.ResizeLeft(_dragStart + e.Vector.X, Bounds.Width);
        RightSplitter.DragStarted += (_, _) => _dragStart = _main?.Layout.RightWidth ?? 0;
        RightSplitter.DragDelta += (_, e) => _main?.Layout.ResizeRight(_dragStart - e.Vector.X, Bounds.Width);
        ShareSplitter.DragStarted += (_, _) => _dragStart = _main?.Layout.LeftShare ?? 0;
        ShareSplitter.DragDelta += (_, e) => _main?.Layout.ResizeShare(_dragStart + e.Vector.Y / Math.Max(1, LeftColumn.Bounds.Height - Gap), LeftColumn.Bounds.Height - Gap);
        BottomSplitter.DragStarted += (_, _) => _dragStart = _main?.Layout.BottomHeight ?? 0;
        BottomSplitter.DragDelta += (_, e) => _main?.Layout.ResizeBottom(_dragStart - e.Vector.Y, CentreColumn.Bounds.Height - Gap);

        foreach (var splitter in new[] { LeftSplitter, RightSplitter, ShareSplitter, BottomSplitter })
            splitter.DragCompleted += (_, _) => _main?.Layout.Remember();

        // The panel last pressed is outlined in the accent, as the mockup's focused panel.
        foreach (var dock in new[] { CharactersDock, AssetsDock, EditorDock, BottomDock, InspectorDock })
            dock.AddHandler(PointerPressedEvent, (_, _) => Focus(dock), RoutingStrategies.Tunnel, handledEventsToo: true);
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
            _main.FocusAssets = () => Avalonia.Threading.Dispatcher.UIThread.Post(AssetsPanel.FocusSearch);
            _main.Assets.PickExport = PickExportAsync;
            _main.Assets.PickImport = PickImportAsync;
            ApplyLayout();
        }
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

        // A closed panel's row or column takes no room, and its gap goes with it: its neighbour takes the room.
        var columns = Docks.ColumnDefinitions;

        columns[0].Width = new GridLength(layout.IsLeftOpen ? layout.LeftWidth : 0);
        columns[1].Width = new GridLength(layout.IsLeftOpen ? Gap : 0);
        columns[3].Width = new GridLength(layout.IsInspectorOpen ? Gap : 0);
        columns[4].Width = new GridLength(layout.IsInspectorOpen ? layout.RightWidth : 0);

        var left = LeftColumn.RowDefinitions;

        left[0].Height = layout.AreBothLeftOpen ? new GridLength(layout.LeftShare, GridUnitType.Star) : new GridLength(layout.IsCharactersOpen ? 1 : 0, GridUnitType.Star);
        left[1].Height = new GridLength(layout.AreBothLeftOpen ? Gap : 0);
        left[2].Height = layout.AreBothLeftOpen ? new GridLength(1 - layout.LeftShare, GridUnitType.Star) : new GridLength(layout.IsAssetsOpen ? 1 : 0, GridUnitType.Star);

        var centre = CentreColumn.RowDefinitions;

        centre[1].Height = new GridLength(layout.IsBottomOpen ? Gap : 0);
        centre[2].Height = new GridLength(layout.IsBottomOpen ? layout.BottomHeight : 0);
    }

    private void Focus(Border dock)
    {
        foreach (var other in new[] { CharactersDock, AssetsDock, EditorDock, BottomDock, InspectorDock })
            other.Classes.Set("Focused", other == dock);
    }

    private void OnRunChoiceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: BotViewModel bot })
            _main?.RunBlock.Choose(bot);

        RunWho.Flyout?.Hide();
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

    private async Task<string?> PickImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("D3B") { Patterns = ["*.json"] }],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void OnDialogsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModels.Dialogs.Current) || sender is not ViewModels.Dialogs dialogs)
            return;

        // ShadUI finds a dialog's view by its context's own type, so each is shown as what it is.
        if (_dialog is not null)
            _dialogManager.Close(_dialog);

        _dialog = dialogs.Current;

        switch (_dialog)
        {
            case QuestionViewModel question:
                _dialogManager.CreateDialog(question).WithMaxWidth(440).Show();
                break;
            case GamePickerViewModel picker:
                _dialogManager.CreateDialog(picker).WithMinWidth(560).WithMaxWidth(720).Show();
                break;
            case NewProfileViewModel profile:
                _dialogManager.CreateDialog(profile).WithMaxWidth(440).Show();
                break;
        }
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
