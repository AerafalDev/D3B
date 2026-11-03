using Avalonia.Controls;
using Avalonia.Interactivity;
using D3B.App.ViewModels.Bottom;

namespace D3B.App.Views;

/// <summary>The trace: the decisions of a character's run, the newest first (D327).</summary>
internal sealed partial class TraceView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="TraceView"/> class.</summary>
    public TraceView()
    {
        InitializeComponent();
    }

    private void OnMenuRowClick(object? sender, RoutedEventArgs e)
    {
        FlyoutRows.Close(sender);
    }

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TraceViewModel trace && (sender as Control)?.DataContext is TraceRunViewModel run)
            trace.Run = run;

        FlyoutRows.Close(sender);
    }
}
