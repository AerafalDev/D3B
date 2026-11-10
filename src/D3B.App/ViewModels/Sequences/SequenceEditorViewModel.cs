using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Settings;
using D3B.App.ViewModels.Studio;
using D3B.Bot.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Sequences;

/// <summary>The editor of a behavior that is a list of steps: a route followed, a harvest, a fight, a wait, in order (D386).</summary>
/// <remarks>
/// Each step is a node of the catalog shown as the sentence it reads, its values tokens that edit them where they stand, as the
/// actions of a route's steps are (D380). The file is the behavior's own, one state of chained actions, which the canvas of 7e opens too.
/// </remarks>
internal sealed partial class SequenceEditorViewModel : ObservableObject, IUndoable
{
    private readonly SequencesViewModel _page;
    private BehaviorAsset _saved;
    private BehaviorSequence _baseline;
    private readonly UndoHistory<BehaviorSequence> _history;
    private List<NodeCall> _steps;
    private StepRepeat _repeat = StepRepeat.Once;
    private IReadOnlyList<(string NodeId, string Name)> _conditions = [];
    private RouteActionPickerViewModel? _picker;

    /// <summary>Gets the behavior's id.</summary>
    public string Id => _saved.Id;

    /// <summary>Gets what keys the list's tab.</summary>
    public string TabKey => _saved.Id;

    /// <summary>Gets the list's name, which the explorer changes.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets the steps, in order.</summary>
    public ObservableCollection<RouteActionViewModel> Steps { get; } = [];

    /// <summary>Gets or sets whether the list starts again once played to its end.</summary>
    public bool Repeats
    {
        get => _repeat.Kind != RepeatKind.Once;
        set
        {
            if (Repeats == value)
                return;

            _repeat = value ? StepRepeat.Forever : StepRepeat.Once;
            ListRepeat.Show(_repeat);
            OnPropertyChanged();
            Changed();
        }
    }

    /// <summary>Gets how the list is played again once played to its end: not at all, a number of times, until a condition holds, for ever.</summary>
    public StepRepeatViewModel ListRepeat { get; private set; }

    /// <summary>Gets whether the list has no step yet.</summary>
    public bool IsEmpty => _steps.Count == 0;

    /// <inheritdoc/>
    public bool CanUndo => _history.CanUndo;

    /// <inheritdoc/>
    public bool CanRedo => _history.CanRedo;

    /// <summary>Gets whether what is edited differs from what was saved.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Gets whether the list can be saved: it changed and every step holds.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool CanSave { get; private set; }

    /// <summary>Gets what is to be looked at in the list, if anything: a route that never ends before a step.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    public partial string? Warning { get; private set; }

    /// <summary>Gets whether something in the list is to be looked at.</summary>
    public bool HasWarning => Warning is not null;

    /// <summary>Gets the steps the menu offers.</summary>
    public IReadOnlyList<RouteActionChoiceViewModel> OfferedSteps { get; private set; } = [];

    /// <summary>Initializes a new instance of the <see cref="SequenceEditorViewModel"/> class.</summary>
    /// <param name="page">The lists' page.</param>
    /// <param name="behavior">The behavior, a list of steps.</param>
    public SequenceEditorViewModel(SequencesViewModel page, BehaviorAsset behavior)
    {
        _page = page;
        _saved = behavior;
        _baseline = Normalized(BehaviorSequences.Read(behavior) ?? BehaviorSequence.Empty);
        _steps = [.. _baseline.Steps];
        _repeat = _baseline.Repeat;
        _history = new UndoHistory<BehaviorSequence>(_baseline, SameSequence);
        Name = behavior.Name;
        Offer();
        ListRepeat = MakeRepeat(_repeat, true, ListRepeatEdited);
        Build();
        CheckSave();
    }

    /// <summary>Takes the file as it changed, unless the editor holds changes of its own.</summary>
    /// <param name="behavior">The behavior, as saved.</param>
    public void Update(BehaviorAsset behavior)
    {
        Name = behavior.Name;

        if (IsDirty || behavior.Equals(_saved) || BehaviorSequences.Read(behavior) is not { } sequence)
            return;

        _saved = behavior;
        _baseline = Normalized(sequence);
        _steps = [.. _baseline.Steps];
        _repeat = sequence.Repeat;
        _history.Reset(Current());
        ListRepeat.Show(_repeat);
        OnPropertyChanged(nameof(Repeats));
        Moved();
        Build();
        CheckSave();
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (_history.TryUndo(out var state))
            Restore(state);
    }

    /// <inheritdoc/>
    public void Redo()
    {
        if (_history.TryRedo(out var state))
            Restore(state);
    }

    /// <summary>Moves a step to another place in the list: a drag, or the buttons and the keys.</summary>
    /// <param name="from">The step's place now.</param>
    /// <param name="to">The place it takes.</param>
    public void MoveTo(int from, int to)
    {
        if (from < 0 || to < 0 || from >= _steps.Count || to >= _steps.Count || from == to)
            return;

        var moved = _steps[from];

        _steps.RemoveAt(from);
        _steps.Insert(to, moved);
        Changed(rebuild: true);
    }

    /// <summary>Says the steps again, in the language chosen.</summary>
    public void Relabel()
    {
        Offer();
        ListRepeat = MakeRepeat(_repeat, true, ListRepeatEdited);
        OnPropertyChanged(nameof(ListRepeat));
        Build();
    }

    [RelayCommand]
    private void AddStep(string? node)
    {
        if (node is null || _page.Nodes is not { } catalog || !catalog.TryGet(node, out var definition))
            return;

        _steps.Add(RouteActionCalls.Create(definition));
        Changed(rebuild: true);
    }

    [RelayCommand]
    private void ChooseStep()
    {
        if (OfferedSteps.Count == 0)
            return;

        _picker = new RouteActionPickerViewModel(_page.Dialogs, _page.Localizer["SequenceAdd"], _page.Localizer["Cancel"], OfferedSteps);
        _page.Dialogs.Show(_picker);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (_page.Library is not { } library)
            return;

        try
        {
            var sequence = Current();

            _saved = await library.SaveAsync(BehaviorSequences.Write(_saved, sequence), CancellationToken.None).ConfigureAwait(true);
            _baseline = sequence;
            Name = _saved.Name;
            IsDirty = false;
            CheckSave();
            _page.Saved(this);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _page.Failed(exception);
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsDirty)
        {
            _page.EndEditing(this);

            return;
        }

        _page.AskToLeave(this, () => SaveCommand.Execute(null), () => _page.EndEditing(this));
    }

    // A value equal to its node's default is not written, so a step reads the same however its file spelled it.
    private BehaviorSequence Normalized(BehaviorSequence sequence)
    {
        if (_page.Nodes is not { } catalog)
            return sequence;

        return sequence with
        {
            Steps =
            [
                .. sequence.Steps.Select(step => catalog.TryGet(step.Node, out var definition) ? RouteActionCalls.Make(definition, RouteActionCalls.Read(definition, step).Settings) with { Repeat = step.Repeat } : step),
            ],
        };
    }

    private BehaviorSequence Current()
    {
        return new BehaviorSequence([.. _steps], _repeat);
    }

    private void AddChosen(string node)
    {
        if (_picker is not null)
        {
            _page.Dialogs.Close(_picker);
            _picker = null;
        }

        AddStep(node);
    }

    [RelayCommand]
    private void Remove(int index)
    {
        if (index < 0 || index >= _steps.Count)
            return;

        _steps.RemoveAt(index);
        Changed(rebuild: true);
    }

    [RelayCommand]
    private void Reorder(ReorderRequest request)
    {
        MoveTo(request.From, request.To);
    }

    private void Move(int index, int delta)
    {
        MoveTo(index, index + delta);
    }

    private void StepEdited(int index, NodeDefinition definition, SettingsEditor editor)
    {
        if (index >= _steps.Count || index >= Steps.Count)
            return;

        _steps[index] = RouteActionCalls.Make(definition, editor.Draft) with { Repeat = _steps[index].Repeat };

        // The forms stay as they are: rebuilding them under the hand that types in one would take its focus.
        Changed(run: editor.TypingRun is { } typing ? "step" + index.ToString(CultureInfo.InvariantCulture) + typing : null);
        Steps[index].Say();
        CheckSave();
    }

    private void Changed(bool rebuild = false, string? run = null)
    {
        _history.Record(Current(), run);
        IsDirty = !Current().IsSame(_baseline);
        Moved();

        if (rebuild)
            Build();

        OnPropertyChanged(nameof(IsEmpty));
        CheckSave();
    }

    private void Restore(BehaviorSequence state)
    {
        _steps = [.. state.Steps];
        _repeat = state.Repeat;
        IsDirty = !state.IsSame(_baseline);
        ListRepeat.Show(_repeat);
        OnPropertyChanged(nameof(Repeats));
        Moved();
        Build();
    }

    private void Moved()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void CheckSave()
    {
        CanSave = IsDirty && !ListRepeat.HasProblems && Steps.All(step => !step.HasProblems && step.Repeat?.HasProblems != true);
        Warning = LoopWarning();
    }

    private string? LoopWarning()
    {
        if (_page.Library is not { } library)
            return null;

        // A loop never ends: whatever follows it would never play.
        for (var index = 0; index < _steps.Count - 1; index++)
        {
            if (_steps[index].Node == RouteActions.Follow && _steps[index].Values.TryGetValue(nameof(RouteFollowValues.Route), out var id) && id is JsonValue text && text.TryGetValue<string>(out var routeId) && library.Find<RouteAsset>(routeId) is { Mode: RouteMode.Loop } route)
                return _page.Localizer.Format("SequenceLoopWarning", route.Name, index + 1);
        }

        return null;
    }

    private void Offer()
    {
        var culture = _page.Localizer.Culture;
        var offered = new List<RouteActionChoiceViewModel>();

        if (_page.Nodes is { } catalog)
        {
            foreach (var node in BehaviorSequences.Offered)
            {
                if (catalog.TryGet(node, out var definition))
                    offered.Add(new RouteActionChoiceViewModel(node, _page.Localizer[NameKeyOf(node)], definition.Help?.In(culture) ?? "", AddChosen));
            }
        }

        OfferedSteps = offered;
        OnPropertyChanged(nameof(OfferedSteps));

        var conditions = new List<(string NodeId, string Name)>();

        if (_page.Nodes is { } nodes)
        {
            foreach (var node in BehaviorSequences.OfferedConditions)
            {
                // The menu says a condition as its sentence, a gap where each value goes.
                if (nodes.TryGet(node, out var definition))
                    conditions.Add((node, SlotPattern().Replace(definition.Sentence.In(culture), "…")));
            }
        }

        _conditions = conditions;
    }

    [GeneratedRegex(@"\{\w+\}", RegexOptions.CultureInvariant)]
    private static partial Regex SlotPattern();

    private StepRepeatViewModel MakeRepeat(StepRepeat repeat, bool allowsForever, Action<StepRepeat, string?> changed)
    {
        return new StepRepeatViewModel(repeat, allowsForever, _conditions, _page.Localizer, ConditionCard, CreateCondition, changed);
    }

    private NodeCall? CreateCondition(string node)
    {
        return _page.Nodes is { } catalog && catalog.TryGet(node, out var definition) ? RouteActionCalls.Create(definition) : null;
    }

    // The condition of an "until" is a sentence with tokens, as a step is, without what moves or removes a step.
    private RouteActionViewModel? ConditionCard(NodeCall call, Action<NodeCall, string?> edited)
    {
        if (_page.Nodes is not { } catalog || !catalog.TryGet(call.Node, out var definition))
            return null;

        var localizer = _page.Localizer;
        var names = new GameNames(_page.Texts, localizer);
        var editor = new SettingsEditor(definition.Values, RouteActionCalls.Read(definition, call).Settings, _page.Game, names, localizer, _page.Provider, [], _page.Dialogs);
        var card = new RouteActionViewModel(0, 1, call.Node, definition, editor, localizer, (kind, id) => NameOf(names, kind, id), _ => { }, (_, _) => { });

        editor.Edited += (_, _) => edited(RouteActionCalls.Make(definition, editor.Draft), editor.TypingRun);

        return card;
    }

    private void StepRepeatEdited(int index, StepRepeat repeat, string? run)
    {
        if (index >= _steps.Count)
            return;

        _steps[index] = _steps[index] with { Repeat = repeat };
        Changed(run: run is null ? null : "step" + index.ToString(CultureInfo.InvariantCulture) + run);
    }

    private void ListRepeatEdited(StepRepeat repeat, string? run)
    {
        _repeat = repeat;
        OnPropertyChanged(nameof(Repeats));
        Changed(run: run);
    }

    private void Build()
    {
        Steps.Clear();

        var localizer = _page.Localizer;
        var names = new GameNames(_page.Texts, localizer);

        for (var index = 0; index < _steps.Count; index++)
        {
            var call = _steps[index];
            NodeDefinition? definition = null;
            SettingsEditor? editor = null;

            if (_page.Nodes is { } catalog && catalog.TryGet(call.Node, out definition))
            {
                var made = definition;
                var at = index;
                var created = new SettingsEditor(made.Values, RouteActionCalls.Read(made, call).Settings, _page.Game, names, localizer, _page.Provider, [], _page.Dialogs);

                created.Edited += (_, _) => StepEdited(at, made, created);

                // A list that plays this one, or this one itself, would never end: it is not offered.
                created.SequenceFilter = candidate => candidate.Id != Id && _page.Library is { } library && !BehaviorSequences.Reaches(candidate, Id, other => library.Find<BehaviorAsset>(other));
                editor = created;
            }

            var place = index;

            Steps.Add(new RouteActionViewModel(index, _steps.Count, call.Node, definition, editor, localizer, (kind, id) => NameOf(names, kind, id), Remove, Move)
            {
                Repeat = MakeRepeat(call.Repeat, false, (repeat, run) => StepRepeatEdited(place, repeat, run)),
            });
        }

        OnPropertyChanged(nameof(IsEmpty));
        CheckSave();
    }

    private string NameOf(GameNames names, SettingKind kind, int id)
    {
        return _page.Game is { } game && game.Options(kind).FirstOrDefault(option => option.Id == id) is { } reference
            ? names.Name(kind, reference)
            : "#" + id.ToString(CultureInfo.InvariantCulture);
    }

    private static bool SameSequence(BehaviorSequence first, BehaviorSequence second)
    {
        return first.IsSame(second);
    }

    private static string NameKeyOf(string node)
    {
        return node switch
        {
            RouteActions.Follow => "SequenceNameFollow",
            BehaviorSequences.Play => "SequenceNamePlay",
            RouteActions.Harvest => "RouteNameHarvest",
            RouteActions.Fight => "RouteNameFight",
            RouteActions.Wait => "RouteNameWait",
            "note" => "SequenceNameNote",
            "pause" => "SequenceNamePause",
            "stop" => "SequenceNameStop",
            _ => "RouteNameBag",
        };
    }
}
