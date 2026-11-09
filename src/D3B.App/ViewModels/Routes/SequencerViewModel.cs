using System.Collections.Immutable;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Controls.Sequencer;
using D3B.Studio.Assets;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>A route's steps set as clips on tracks, a track for each kind of action (D395, D396).</summary>
/// <remarks>
/// <para>
/// It reads the route editor's steps as clips (<see cref="RouteClips"/>) and shows them under the world: the route's file is
/// as it was (D378), a clip being the longest run of steps that hold an equal action. It follows the editor as the route
/// changes and holds only what the editor does not: the tracks the user added, which hold no clip yet, and the clip selected.
/// </para>
/// </remarks>
internal sealed partial class SequencerViewModel : ObservableObject
{
    private readonly RouteEditorViewModel _route;
    private readonly RoutesViewModel _page;
    private readonly List<string> _added = [];
    private ImmutableArray<RouteClip> _clips = [];
    private RouteClip? _selected;

    /// <summary>Gets what is drawn.</summary>
    [ObservableProperty]
    public partial SequencerScene Scene { get; private set; } = SequencerScene.Empty;

    /// <summary>Gets the clip selected, by its place in the scene, or -1 for none.</summary>
    [ObservableProperty]
    public partial int SelectedClip { get; private set; } = -1;

    /// <summary>Gets the kinds of action a track can still be added for, which the palette offers.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<RouteActionChoiceViewModel> Palette { get; private set; } = [];

    /// <summary>Gets the steps selected, as the route editor holds them.</summary>
    public IReadOnlyList<int> SelectedSteps => _route.SelectedSteps;

    /// <summary>Gets whether the route has a step.</summary>
    public bool HasSteps => Scene.Steps.Count > 0;

    /// <summary>Gets whether the palette has a kind left to add.</summary>
    public bool HasPalette => Palette.Count > 0;

    /// <summary>Gets the route's name.</summary>
    public string RouteName => _route.Name;

    /// <summary>Gets the clip selected, or <see langword="null"/>.</summary>
    public RouteClip? Selected => _selected;

    /// <summary>Initializes a new instance of the <see cref="SequencerViewModel"/> class.</summary>
    /// <param name="route">The route's editor.</param>
    /// <param name="page">The routes' page, which holds what the editors read.</param>
    public SequencerViewModel(RouteEditorViewModel route, RoutesViewModel page)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(page);

        _route = route;
        _page = page;
        route.Rebuilt += Rebuild;
        route.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(RouteEditorViewModel.SelectedSteps))
                OnPropertyChanged(nameof(SelectedSteps));
            else if (e.PropertyName is nameof(RouteEditorViewModel.Name))
                OnPropertyChanged(nameof(RouteName));
        };
        Rebuild();
    }

    /// <summary>Chooses a step from the ruler: Control adds it to the selection or takes it off, Shift selects up to it, else it is chosen alone and shown on the map.</summary>
    /// <param name="step">The step, from 0.</param>
    /// <param name="modifiers">The keys held.</param>
    public void PressStep(int step, KeyModifiers modifiers)
    {
        Select(null);

        if (modifiers.HasFlag(KeyModifiers.Shift))
            _route.ExtendTo(step);
        else if (modifiers.HasFlag(KeyModifiers.Control))
            _route.ToggleStep(step);
        else
            _route.Reveal(step);
    }

    /// <summary>Selects a clip, the steps selected left as they are.</summary>
    /// <param name="id">The clip's place in the scene.</param>
    public void PressClip(int id)
    {
        Select(id >= 0 && id < _clips.Length ? _clips[id] : null);
    }

    /// <summary>Lets go of the clip selected.</summary>
    public void ClearClip()
    {
        Select(null);
    }

    /// <summary>Shows a track for a kind of action, empty until a clip is drawn on it.</summary>
    /// <param name="node">The action's node id.</param>
    public void AddTrack(string node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_added.Contains(node, StringComparer.Ordinal))
            return;

        _added.Add(node);
        Rebuild();
    }

    private static int Rank(string node)
    {
        var at = RouteActions.Offered.IndexOf(node);

        return at < 0 ? RouteActions.Offered.Length : at;
    }

    private void Select(RouteClip? clip)
    {
        _selected = clip;
        SelectedClip = clip is null ? -1 : _clips.IndexOf(clip);
    }

    private void Rebuild()
    {
        var anchors = _route.CurrentAnchors;
        var clips = RouteClips.Derive(anchors);
        var steps = new List<SequencerStep>(anchors.Count);

        for (var i = 0; i < anchors.Count; i++)
        {
            var row = i < _route.Steps.Count ? _route.Steps[i] : null;

            steps.Add(new SequencerStep((i + 1).ToString(_page.Localizer.Culture), row?.Place ?? "", row?.Zone ?? ""));
        }

        var nodes = clips.Select(clip => clip.Call.Node).Concat(_added).Distinct(StringComparer.Ordinal).OrderBy(Rank).ThenBy(node => node, StringComparer.Ordinal);
        var tracks = new List<SequencerTrack>();

        foreach (var node in nodes)
        {
            var own = new List<SequencerClip>();

            foreach (var (clip, id) in clips.Select((clip, id) => (clip, id)).Where(pair => pair.clip.Call.Node == node))
            {
                var (text, problems) = Sentence(clip, anchors);

                own.Add(new SequencerClip(id, clip.First, clip.Last, clip.Lane, text, problems));
            }

            tracks.Add(new SequencerTrack(node, NameOf(node), RouteActionKinds.OfNode(node), Math.Max(1, own.Count == 0 ? 1 : own.Max(clip => clip.Lane) + 1), own));
        }

        _clips = clips;
        Scene = new SequencerScene(steps, tracks);
        SelectedClip = Retarget();
        Palette = [.. _route.OfferedActions.Where(item => !tracks.Any(track => track.Node == item.Node)).Select(item => new RouteActionChoiceViewModel(item.Node, item.Name, item.Help, AddTrack))];
    }

    // The clip selected is the one of the same action and layer that still covers some of its steps.
    private int Retarget()
    {
        if (_selected is not { } was)
            return -1;

        var now = _clips.FirstOrDefault(clip => clip.Layer == was.Layer && RouteAnchor.SameCall(clip.Call, was.Call) && clip.First <= was.Last && clip.Last >= was.First);

        _selected = now;

        return now is null ? -1 : _clips.IndexOf(now);
    }

    private string NameOf(string node)
    {
        return RouteActions.Offered.Contains(node) ? _page.Localizer[RouteTexts.ActionNameKey(node)] : node;
    }

    private (string Text, bool HasProblems) Sentence(RouteClip clip, IReadOnlyList<RouteAnchor> anchors)
    {
        var step = new RouteAnchor { MapId = anchors[clip.First].MapId, Actions = [clip.Call] };
        var rows = RouteActionForms.Build(_page.Services!, _page.Localizer, _page.Dialogs, step, (_, _, _) => { }, _ => { }, (_, _) => { });

        return rows.Count == 0 ? (NameOf(clip.Call.Node), false) : (rows[0].Sentence, rows[0].HasProblems);
    }

    partial void OnSceneChanged(SequencerScene value)
    {
        OnPropertyChanged(nameof(HasSteps));
    }

    partial void OnPaletteChanged(IReadOnlyList<RouteActionChoiceViewModel> value)
    {
        OnPropertyChanged(nameof(HasPalette));
    }
}
