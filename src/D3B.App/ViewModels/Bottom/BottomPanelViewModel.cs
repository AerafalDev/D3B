using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.ViewModels.Inspector;
using D3B.App.ViewModels.Logs;
using D3B.App.ViewModels.Statistics;
using D3B.App.ViewModels.Studio;

namespace D3B.App.ViewModels.Bottom;

/// <summary>The bottom panel: the journal, the trace, the problems, the protocol and the statistics, a tab each (D327).</summary>
/// <remarks>Only the tab shown reads the host, and only while the panel is open: the others read what came since once shown again.</remarks>
internal sealed partial class BottomPanelViewModel : ObservableObject
{
    private readonly StudioLayout _layout;
    private readonly Func<IServiceProvider?> _services;

    /// <summary>Gets the journal.</summary>
    public LogsViewModel Logs { get; }

    /// <summary>Gets the protocol.</summary>
    public InspectorViewModel Inspector { get; }

    /// <summary>Gets the statistics.</summary>
    public StatisticsViewModel Statistics { get; }

    /// <summary>Gets the tab shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJournalShown), nameof(IsTraceShown), nameof(IsProblemsShown), nameof(IsProtocolShown), nameof(IsStatisticsShown))]
    public partial BottomTab Tab { get; private set; }

    /// <summary>Gets whether the journal shows.</summary>
    public bool IsJournalShown => Tab is BottomTab.Journal;

    /// <summary>Gets whether the trace shows.</summary>
    public bool IsTraceShown => Tab is BottomTab.Trace;

    /// <summary>Gets whether the problems show.</summary>
    public bool IsProblemsShown => Tab is BottomTab.Problems;

    /// <summary>Gets whether the protocol shows.</summary>
    public bool IsProtocolShown => Tab is BottomTab.Protocol;

    /// <summary>Gets whether the statistics show.</summary>
    public bool IsStatisticsShown => Tab is BottomTab.Statistics;

    /// <summary>Initializes a new instance of the <see cref="BottomPanelViewModel"/> class.</summary>
    /// <param name="layout">The docks, which open and close the panel.</param>
    /// <param name="services">Gets the host's services, once it runs.</param>
    /// <param name="logs">The journal.</param>
    /// <param name="inspector">The protocol.</param>
    /// <param name="statistics">The statistics.</param>
    public BottomPanelViewModel(StudioLayout layout, Func<IServiceProvider?> services, LogsViewModel logs, InspectorViewModel inspector, StatisticsViewModel statistics)
    {
        _layout = layout;
        _services = services;
        Logs = logs;
        Inspector = inspector;
        Statistics = statistics;
    }

    /// <summary>Reads the tab shown again, if the panel is open.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (!_layout.IsBottomOpen)
            return;

        switch (Tab)
        {
            case BottomTab.Journal:
                Logs.Refresh(services);
                break;
            case BottomTab.Protocol:
                Inspector.Refresh(services);
                break;
            case BottomTab.Statistics:
                Statistics.Refresh(services);
                break;
        }
    }

    /// <summary>Shows a tab, the panel opened if it was closed.</summary>
    /// <param name="tab">The tab.</param>
    [RelayCommand]
    public void Show(BottomTab tab)
    {
        Tab = tab;

        if (!_layout.IsBottomOpen)
            _layout.Show(StudioPanel.Bottom, true);

        Refresh(_services());
    }

    /// <summary>Shows the panel, or closes it: Ctrl J.</summary>
    [RelayCommand]
    public void Toggle()
    {
        _layout.Toggle(StudioPanel.Bottom);
        Refresh(_services());
    }

    /// <summary>Closes the panel, its neighbour taking the room.</summary>
    [RelayCommand]
    public void Close()
    {
        _layout.Close(StudioPanel.Bottom);
    }
}
