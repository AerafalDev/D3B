using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.ViewModels.Inspector;
using D3B.App.ViewModels.Logs;
using D3B.App.ViewModels.Routes;
using D3B.App.Localization;
using D3B.App.ViewModels.Studio;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.Bottom;

/// <summary>The bottom panel: the journal, the trace, the problems and the protocol, a tab each; the statistics have a tab of the editor (D327, D516).</summary>
/// <remarks>
/// Only the tab shown reads the host, and only while the panel is open: the others read what came since once shown again.
/// The problems are read whichever tab shows, since the tabs' row counts them.
/// </remarks>
internal sealed partial class BottomPanelViewModel : ObservableObject
{
    private readonly StudioLayout _layout;
    private readonly Func<IServiceProvider?> _services;

    /// <summary>Gets the journal.</summary>
    public LogsViewModel Logs { get; }

    /// <summary>Gets the trace.</summary>
    public TraceViewModel Trace { get; }

    /// <summary>Gets the problems.</summary>
    public ProblemsViewModel Problems { get; }

    /// <summary>Gets the protocol.</summary>
    public InspectorViewModel Inspector { get; }

    /// <summary>Gets the tab shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSequencerShown), nameof(IsJournalShown), nameof(IsTraceShown), nameof(IsProblemsShown), nameof(IsProtocolShown), nameof(IsJournalOpen))]
    public partial BottomTab Tab { get; private set; }

    /// <summary>Gets or sets the character the run block chose, whose run the trace shows until another is chosen in it.</summary>
    [ObservableProperty]
    public partial CharacterKey? Chosen { get; set; }

    /// <summary>Gets or sets what says why the host did not start, once it failed, which the problems tell.</summary>
    public Func<string?>? HostFailure { get; set; }

    /// <summary>Gets the Sequencer of the route selected, or <see langword="null"/> when none is: its tab is there only then (D395).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSequencer))]
    public partial SequencerViewModel? Sequencer { get; private set; }

    /// <summary>Gets whether a route is selected, so that the Sequencer's tab is shown.</summary>
    public bool HasSequencer => Sequencer is not null;

    /// <summary>Gets whether the Sequencer shows.</summary>
    public bool IsSequencerShown => Tab is BottomTab.Sequencer;

    /// <summary>Gets whether the journal shows.</summary>
    public bool IsJournalShown => Tab is BottomTab.Journal;

    /// <summary>Gets whether the trace shows.</summary>
    public bool IsTraceShown => Tab is BottomTab.Trace;

    /// <summary>Gets whether the problems show.</summary>
    public bool IsProblemsShown => Tab is BottomTab.Problems;

    /// <summary>Gets whether the protocol shows.</summary>
    public bool IsProtocolShown => Tab is BottomTab.Protocol;

    /// <summary>Gets whether the panel shows the journal, which the activity bar's entry says pressed (D513).</summary>
    public bool IsJournalOpen => _layout.IsBottomOpen && IsJournalShown;

    /// <summary>Gets whether the protocol's tab is there: only with the advanced tools (D513).</summary>
    public bool HasProtocol => _layout.AdvancedTools;

    /// <summary>Initializes a new instance of the <see cref="BottomPanelViewModel"/> class.</summary>
    /// <param name="layout">The docks, which open and close the panel.</param>
    /// <param name="services">Gets the host's services, once it runs.</param>
    /// <param name="logs">The journal.</param>
    /// <param name="inspector">The protocol.</param>
    /// <param name="localizer">The app's texts.</param>
    public BottomPanelViewModel(StudioLayout layout, Func<IServiceProvider?> services, LogsViewModel logs, InspectorViewModel inspector, Localizer localizer)
    {
        _layout = layout;
        _services = services;
        Logs = logs;
        Trace = new TraceViewModel(localizer);
        Problems = new ProblemsViewModel(localizer) { ShowHostJournal = ShowHostJournal };
        Inspector = inspector;
        layout.PropertyChanged += (_, e) => FollowLayout(e.PropertyName);
    }

    /// <summary>Reads the problems, which the status bar counts, then the tab shown again if the panel is open.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        // Always: the status bar counts them (D541), and they are read again only when the project, the accounts or the host change.
        Problems.Refresh(services, HostFailure?.Invoke());

        if (!_layout.IsBottomOpen)
            return;

        switch (Tab)
        {
            case BottomTab.Journal:
                Logs.Refresh(services);
                break;
            case BottomTab.Trace:
                Trace.Refresh(services);
                break;
            case BottomTab.Protocol:
                Inspector.Refresh(services);
                break;
        }
    }

    /// <summary>Follows the route selected: its Sequencer shows, in a tab the panel shows at once; without one the tab goes and another is shown.</summary>
    /// <param name="route">The route's editor, or <see langword="null"/> when no route is selected.</param>
    public void Follow(RouteEditorViewModel? route)
    {
        var sequencer = route?.Sequencer;

        if (ReferenceEquals(sequencer, Sequencer))
            return;

        var first = Sequencer is null;

        Sequencer = sequencer;

        // Selecting a route shows its Sequencer; going from one route to another leaves the tab the user is on.
        if (sequencer is not null && first)
        {
            Show(BottomTab.Sequencer);
        }
        else if (sequencer is null && Tab is BottomTab.Sequencer)
        {
            Tab = BottomTab.Journal;
            Refresh(_services());
        }
    }

    /// <summary>Shows a character's run in the trace, as its tab's "See the trace" asks.</summary>
    /// <param name="key">The character.</param>
    public void ShowTrace(CharacterKey key)
    {
        Trace.Follow(key);
        Show(BottomTab.Trace);
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

    /// <summary>Shows a tab, or hides the panel when it shows that tab already, as the activity bar's entries do (D513).</summary>
    /// <param name="tab">The tab.</param>
    [RelayCommand]
    public void ToggleTab(BottomTab tab)
    {
        if (_layout.IsBottomOpen && Tab == tab)
            Close();
        else
            Show(tab);
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

    private void FollowLayout(string? property)
    {
        switch (property)
        {
            case nameof(StudioLayout.IsBottomOpen):
                OnPropertyChanged(nameof(IsJournalOpen));
                break;
            case nameof(StudioLayout.AdvancedTools):
                OnPropertyChanged(nameof(HasProtocol));

                // The protocol hidden while it shows leaves the journal in its place.
                if (!HasProtocol && Tab is BottomTab.Protocol)
                {
                    Tab = BottomTab.Journal;
                    Refresh(_services());
                }

                break;
        }
    }

    private void ShowHostJournal()
    {
        Show(BottomTab.Journal);
        Logs.ShowHost();
    }

    partial void OnChosenChanged(CharacterKey? value)
    {
        Trace.Follow(value);
    }
}
