using Avalonia.Controls;
using Avalonia.Interactivity;

namespace D3B.App.Views;

/// <summary>The statistics: what the runs D3B kept brought, by character and period (D146, D327).</summary>
internal sealed partial class StatisticsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="StatisticsView"/> class.</summary>
    public StatisticsView()
    {
        InitializeComponent();
    }

    private void OnMenuRowClick(object? sender, RoutedEventArgs e)
    {
        FlyoutRows.Close(sender);
    }
}
