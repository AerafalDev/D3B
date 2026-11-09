using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls.Sequencer;
using D3B.Studio.Assets;
using D3B.App.ViewModels.Settings;
using D3B.Studio.Behaviors;
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
    private SequencerGesture _gesture;
    private ImmutableArray<RouteAnchor> _before = [];
    private RouteClip? _held;
    private NodeCall? _drawn;
    private SequencerEdge _edge;
    private int _from;
    private string _cardsKey = "";
    private bool _editing;
    private List<NodeCall> _common = [];

    /// <summary>Gets what is drawn.</summary>
    [ObservableProperty]
    public partial SequencerScene Scene { get; private set; } = SequencerScene.Empty;

    /// <summary>Gets the clip selected, by its place in the scene, or -1 for none.</summary>
    [ObservableProperty]
    public partial int SelectedClip { get; private set; } = -1;

    /// <summary>Gets the kinds of action a track can still be added for, which the palette offers.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<RouteActionChoiceViewModel> Palette { get; private set; } = [];

    /// <summary>Gets the values of the clip selected, as the sentence of its action: they are set once for every step the clip covers.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClip))]
    public partial RouteActionViewModel? ClipCard { get; private set; }

    /// <summary>Gets the actions every step selected holds, each set once for all of them.</summary>
    public ObservableCollection<RouteActionViewModel> CommonActions { get; } = [];

    /// <summary>Gets whether a clip is selected.</summary>
    public bool HasClip => ClipCard is not null;

    /// <summary>Gets whether several steps are selected.</summary>
    public bool HasSeveralSteps => _route.HasSeveralSteps;

    /// <summary>Gets whether several steps are selected and no action is on all of them.</summary>
    public bool HasNoCommonAction => HasSeveralSteps && CommonActions.Count == 0;

    /// <summary>Gets the steps selected, as the route editor holds them.</summary>
    public IReadOnlyList<int> SelectedSteps => _route.SelectedSteps;

    /// <summary>Gets whether the route has a step.</summary>
    public bool HasSteps => Scene.Steps.Count > 0;

    /// <summary>Gets whether the palette has a kind left to add.</summary>
    public bool HasPalette => Palette.Count > 0;

    /// <summary>Gets what is selected, said for a screen reader and under the eyes: the clip, else the steps.</summary>
    public string Status
    {
        get
        {
            if (_selected is not { } clip)
                return StepsStatus;

            var name = NameOf(clip.Call.Node);

            return clip.Length == 1 ? _page.Localizer.Format("SequencerClipStatusOne", name, clip.First + 1) : _page.Localizer.Format("SequencerClipStatus", name, clip.First + 1, clip.Last + 1);
        }
    }

    /// <summary>Gets the steps selected, said in words.</summary>
    public string StepsStatus
    {
        get
        {
            return _route.SelectedSteps.Count switch
            {
                0 => "",
                1 => _page.Localizer.Format("SequencerStepStatus", _route.SelectedSteps[0] + 1, _route.CurrentAnchors.Count),
                var several => _page.Localizer.Format("SequencerStepsStatus", several),
            };
        }
    }

    /// <summary>Gets the name a screen reader says for the grid.</summary>
    public string AutomationName => _page.Localizer.Format("SequencerAutomation", _route.Name);

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
            {
                OnPropertyChanged(nameof(SelectedSteps));
                OnPropertyChanged(nameof(HasSeveralSteps));
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StepsStatus));
                BuildCards();
            }
            else if (e.PropertyName is nameof(RouteEditorViewModel.Name))
            {
                OnPropertyChanged(nameof(RouteName));
                OnPropertyChanged(nameof(AutomationName));
            }
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

    /// <summary>Answers a key pressed on the grid.</summary>
    /// <remarks>
    /// Left and Right choose the step before and after (Shift reaches out), Home and End the first and the last, Ctrl A every
    /// step; Up and Down choose the clips covering the step chosen one after the other, Escape lets go of the clip; Alt with Left
    /// and Right moves the clip, with Shift its last edge, with Control its first; Ctrl D copies it right after itself, and
    /// Delete takes it off.
    /// </remarks>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The keys held.</param>
    /// <returns>Whether the key did something.</returns>
    public bool PressKey(Key key, KeyModifiers modifiers)
    {
        var last = _route.CurrentAnchors.Count - 1;
        var active = _route.SelectedIndex;
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var control = modifiers.HasFlag(KeyModifiers.Control);

        if (last < 0)
            return false;

        if (modifiers.HasFlag(KeyModifiers.Alt) && key is Key.Left or Key.Right)
            return ShiftClip(key is Key.Left ? -1 : 1, shift, control);

        switch (key)
        {
            case Key.Left or Key.Right:
                GoTo(active < 0 ? (key is Key.Right ? 0 : last) : Math.Clamp(active + (key is Key.Left ? -1 : 1), 0, last), shift);

                return true;
            case Key.Home or Key.End:
                GoTo(key is Key.Home ? 0 : last, shift);

                return true;
            case Key.A when control:
                Select(null);
                _route.SelectSteps(Enumerable.Range(0, last + 1));

                return true;
            case Key.Up or Key.Down:
                return NextClip(key is Key.Down ? 1 : -1);
            case Key.D when control:
                return DuplicateClip();
            case Key.Escape when _selected is not null:
                Select(null);

                return true;
            case Key.Delete or Key.Back when _selected is not null:
                RemoveSelectedClip();

                return true;
            default:
                return false;
        }
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

    /// <summary>Starts a clip's gesture: a press on its edge stretches it, on its body moves it, or copies it with Shift. The clip is selected; the steps change as the pointer moves.</summary>
    /// <param name="id">The clip's place in the scene.</param>
    /// <param name="edge">The part pressed.</param>
    /// <param name="step">The step under the pointer.</param>
    /// <param name="modifiers">The keys held.</param>
    public void BeginClip(int id, SequencerEdge edge, int step, KeyModifiers modifiers)
    {
        if (id < 0 || id >= _clips.Length)
            return;

        var clip = _clips[id];

        Select(clip);
        Begin(edge is not SequencerEdge.None ? SequencerGesture.Stretch : modifiers.HasFlag(KeyModifiers.Shift) ? SequencerGesture.Copy : SequencerGesture.Move, step);
        _held = clip;
        _edge = edge;
    }

    /// <summary>Starts drawing a clip on a track from an empty cell: the clip of the kind, with the node's own values, covers the steps the pointer crosses.</summary>
    /// <param name="track">The track's place in the scene.</param>
    /// <param name="step">The step pressed.</param>
    public void BeginDraw(int track, int step)
    {
        if (track < 0 || track >= Scene.Tracks.Count || _page.Services?.Nodes is not { } catalog || !catalog.TryGet(Scene.Tracks[track].Node, out var definition))
            return;

        Begin(SequencerGesture.Draw, step);
        _drawn = RouteActionCalls.Create(definition);
        Drag(step);
    }

    /// <summary>Moves the gesture on to the step the pointer is over, the steps changed from what they were before it began.</summary>
    /// <param name="step">The step, from 0.</param>
    public void Drag(int step)
    {
        if (_gesture is SequencerGesture.None)
            return;

        var last = _before.Length - 1;
        var (call, layer, first, to) = _gesture switch
        {
            SequencerGesture.Draw => (_drawn!, 0, Math.Min(_from, step), Math.Max(_from, step)),
            SequencerGesture.Stretch when _edge is SequencerEdge.Left => (_held!.Call, _held.Layer, Math.Min(step, _held.Last), _held.Last),
            SequencerGesture.Stretch => (_held!.Call, _held.Layer, _held.First, Math.Max(step, _held.First)),
            _ => Shifted(_held!, step - _from, last),
        };

        first = Math.Clamp(first, 0, last);
        to = Math.Clamp(to, first, last);
        _selected = new RouteClip(call, first, to, layer, 0);
        _route.ReplaceAnchors(_gesture switch
        {
            SequencerGesture.Move => RouteClips.Reshape(_before, _held!, first, to),
            SequencerGesture.Stretch => RouteClips.Reshape(_before, _held!, first, to),
            _ => RouteClips.Draw(_before, call, first, to, layer),
        });
    }

    /// <summary>Ends the gesture: what it changed is one change to take back.</summary>
    public void EndGesture()
    {
        if (_gesture is SequencerGesture.None)
            return;

        _gesture = SequencerGesture.None;
        _held = null;
        _drawn = null;
        _route.EndGesture();
    }

    /// <summary>Makes the clip selected cover the whole route.</summary>
    [RelayCommand]
    public void ExtendClip()
    {
        if (_selected is not { } clip)
            return;

        _selected = clip with { First = 0, Last = _route.CurrentAnchors.Count - 1 };
        _route.ReplaceAnchors(RouteClips.Reshape(_route.CurrentAnchors, clip, 0, _route.CurrentAnchors.Count - 1));
    }

    /// <summary>Takes the clip selected off every step it covers: Delete.</summary>
    [RelayCommand]
    public void RemoveSelectedClip()
    {
        if (_selected is not { } clip || clip.Last >= _route.CurrentAnchors.Count)
            return;

        _selected = null;
        _route.ReplaceAnchors(RouteClips.Remove(_route.CurrentAnchors, clip));
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

    // The forms of the clip selected and of the actions the steps selected share are made again when what they show changed, and not while a value is typed in them.
    private void BuildCards()
    {
        if (_editing)
            return;

        var anchors = _route.CurrentAnchors;
        var steps = _route.SelectedSteps;
        var common = steps.Count > 1 && steps.All(step => step < anchors.Count) ? RouteClips.CommonCalls(anchors, steps) : [];
        var key = (_selected is { } clip && clip.First < anchors.Count ? KeyOf(clip.Call) + "/" + clip.Layer.ToString(_page.Localizer.Culture) + "@" + anchors[clip.First].MapId.ToString(_page.Localizer.Culture) : "") + "#" + string.Join(',', steps) + "#" + string.Join('/', common.Select(KeyOf));

        if (key == _cardsKey)
            return;

        _cardsKey = key;
        ClipCard = _selected is { } selected && selected.Last < anchors.Count ? Card(selected.Call, anchors[selected.First].MapId, ClipEdited, _ => RemoveSelectedClip()) : null;
        CommonActions.Clear();
        _common = [.. common];

        for (var i = 0; i < common.Length; i++)
        {
            // Each card is a step of one action to the form, which says index 0: its place among the cards is the one told here.
            var place = i;

            CommonActions.Add(Card(common[i], anchors[steps[0]].MapId, (_, definition, editor) => CommonEdited(place, definition, editor), _ => { }));
        }

        OnPropertyChanged(nameof(HasNoCommonAction));
    }

    private RouteActionViewModel Card(NodeCall call, long map, Action<int, NodeDefinition, SettingsEditor> edited, Action<int> remove)
    {
        var step = new RouteAnchor { MapId = map, Actions = [call] };
        var rows = RouteActionForms.Build(_page.Services!, _page.Localizer, _page.Dialogs, step, edited, remove, (_, _) => { });

        return rows[0];
    }

    private void ClipEdited(int index, NodeDefinition definition, SettingsEditor editor)
    {
        if (_selected is not { } clip || ClipCard is not { } card)
            return;

        var made = RouteActionCalls.Make(definition, editor.Draft);

        if (RouteAnchor.SameCall(clip.Call, made))
            return;

        Edit(() =>
        {
            _selected = clip with { Call = made };
            _route.ReplaceAnchors(RouteClips.Revalue(_route.CurrentAnchors, clip, made));
        });
        card.Say();
        _cardsKey = "";
    }

    private void CommonEdited(int index, NodeDefinition definition, SettingsEditor editor)
    {
        if (index < 0 || index >= _common.Count || index >= CommonActions.Count)
            return;

        var made = RouteActionCalls.Make(definition, editor.Draft);
        var was = _common[index];

        if (RouteAnchor.SameCall(was, made))
            return;

        Edit(() => _route.ReplaceAnchors(RouteClips.RevalueOn(_route.CurrentAnchors, _route.SelectedSteps, was, made)));
        _common[index] = made;
        CommonActions[index].Say();
        _cardsKey = "";
    }

    // The value typed stays in the form the hand is in: the cards are not made again under it.
    private void Edit(Action change)
    {
        _editing = true;

        try
        {
            change();
        }
        finally
        {
            _editing = false;
        }
    }

    private static string KeyOf(NodeCall call)
    {
        return call.Node + string.Concat(call.Values.Select(pair => "|" + pair.Key + "=" + pair.Value?.ToJsonString()));
    }

    private void GoTo(int step, bool extend)
    {
        if (extend)
            _route.ExtendTo(step);
        else
            _route.Reveal(step);
    }

    private bool NextClip(int direction)
    {
        var active = _route.SelectedIndex;
        var here = active < 0 ? _clips : [.. _clips.Where(clip => clip.Covers(active))];
        var candidates = here.IsEmpty ? _clips : here;

        if (candidates.IsEmpty)
            return false;

        var at = _selected is null ? -1 : candidates.IndexOf(_selected);
        var next = at < 0 ? (direction > 0 ? 0 : candidates.Length - 1) : Math.Clamp(at + direction, 0, candidates.Length - 1);

        Select(candidates[next]);

        return true;
    }

    private bool ShiftClip(int delta, bool lastEdge, bool firstEdge)
    {
        if (_selected is not { } clip)
            return false;

        var last = _route.CurrentAnchors.Count - 1;
        var (first, to) = lastEdge ? (clip.First, Math.Clamp(clip.Last + delta, clip.First, last)) : firstEdge ? (Math.Clamp(clip.First + delta, 0, clip.Last), clip.Last) : (clip.First + delta, clip.Last + delta);

        if (first < 0 || to > last || (first, to) == (clip.First, clip.Last))
            return true;

        _selected = clip with { First = first, Last = to };
        _route.BeginGesture();
        _route.ReplaceAnchors(RouteClips.Reshape(_route.CurrentAnchors, clip, first, to));
        _route.EndGesture();

        return true;
    }

    private bool DuplicateClip()
    {
        if (_selected is not { } clip)
            return false;

        var last = _route.CurrentAnchors.Count - 1;
        var first = Math.Min(clip.Last + 1, last - clip.Length + 1);

        if (first < 0)
            return true;

        _selected = clip with { First = clip.First, Last = Math.Max(clip.Last, first + clip.Length - 1) };
        _route.BeginGesture();
        _route.ReplaceAnchors(RouteClips.Draw(_route.CurrentAnchors, clip.Call, first, first + clip.Length - 1, clip.Layer));
        _route.EndGesture();

        return true;
    }

    private static (NodeCall Call, int Layer, int First, int Last) Shifted(RouteClip clip, int delta, int lastStep)
    {
        var first = Math.Clamp(clip.First + delta, 0, Math.Max(0, lastStep - clip.Length + 1));

        return (clip.Call, clip.Layer, first, first + clip.Length - 1);
    }

    private static int Rank(string node)
    {
        var at = RouteActions.Offered.IndexOf(node);

        return at < 0 ? RouteActions.Offered.Length : at;
    }

    private void Begin(SequencerGesture gesture, int step)
    {
        EndGesture();
        _route.BeginGesture();
        _before = [.. _route.CurrentAnchors];
        _gesture = gesture;
        _from = step;
    }

    private void Select(RouteClip? clip)
    {
        _selected = clip;
        SelectedClip = clip is null ? -1 : _clips.IndexOf(clip);
        OnPropertyChanged(nameof(Status));
        BuildCards();
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
        OnPropertyChanged(nameof(Status));
        BuildCards();
        Palette = [.. Offered().Where(item => !tracks.Any(track => track.Node == item.Node))];
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

    // The kinds of action the host knows, in the order of the editor's menu.
    private IEnumerable<RouteActionChoiceViewModel> Offered()
    {
        var catalog = _page.Services?.Nodes;

        foreach (var node in RouteActions.Offered)
        {
            if (catalog is not null && catalog.TryGet(node, out var definition))
                yield return new RouteActionChoiceViewModel(node, NameOf(node), definition.Help?.In(_page.Localizer.Culture) ?? "", AddTrack);
        }
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
