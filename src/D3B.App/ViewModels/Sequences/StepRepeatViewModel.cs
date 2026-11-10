using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;
using D3B.App.ViewModels.Routes;
using D3B.Studio.Behaviors;

namespace D3B.App.ViewModels.Sequences;

/// <summary>How a step of a list, or the list, is repeated: once, a number of times, until a condition holds, for ever (D421).</summary>
/// <remarks>
/// Said as a token that opens its choices where it stands, as the values of an action are. The condition of an "until" is a node
/// of the catalog edited with the same sentence and tokens as a step.
/// </remarks>
internal sealed partial class StepRepeatViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly Func<NodeCall, Action<NodeCall, string?>, RouteActionViewModel?> _card;
    private readonly Func<string, NodeCall?> _create;
    private readonly Action<StepRepeat, string?> _changed;
    private RepeatKind _kind;
    private int _count = 2;
    private NodeCall? _call;

    /// <summary>Gets whether "for ever" is offered: it is for a list, whose end the run waits for, not for a step.</summary>
    public bool AllowsForever { get; }

    /// <summary>Gets the conditions an "until" can wait on.</summary>
    public IReadOnlyList<RepeatConditionChoiceViewModel> Conditions { get; }

    /// <summary>Gets or sets whether the step is played once.</summary>
    public bool IsOnce
    {
        get => _kind == RepeatKind.Once;
        set => Choose(value, RepeatKind.Once);
    }

    /// <summary>Gets or sets whether the step is played a number of times.</summary>
    public bool IsTimes
    {
        get => _kind == RepeatKind.Times;
        set => Choose(value, RepeatKind.Times);
    }

    /// <summary>Gets or sets whether the step is played until a condition holds.</summary>
    public bool IsUntil
    {
        get => _kind == RepeatKind.Until;
        set => Choose(value, RepeatKind.Until);
    }

    /// <summary>Gets or sets whether the list is played for ever.</summary>
    public bool IsForever
    {
        get => _kind == RepeatKind.Forever;
        set => Choose(value, RepeatKind.Forever);
    }

    /// <summary>Gets or sets how many times: from two, to the most a repeat can ask for.</summary>
    public int Count
    {
        get => _count;
        set
        {
            var kept = Math.Clamp(value, 2, StepRepeat.MostTimes);

            if (_count == kept)
            {
                // A number out of bounds was brought back in: the box says so.
                OnPropertyChanged();

                return;
            }

            _count = kept;
            OnPropertyChanged();
            Summarize();

            if (_kind == RepeatKind.Times)
                _changed(Current(), "repeat-count");
        }
    }

    /// <summary>Gets whether the count is asked for.</summary>
    public bool HasCount => _kind == RepeatKind.Times;

    /// <summary>Gets whether the condition is shown: the repeat is an "until".</summary>
    public bool HasCondition => _kind == RepeatKind.Until && Condition is not null;

    /// <summary>Gets whether the condition of an "until" lacks a value.</summary>
    public bool HasProblems => _kind == RepeatKind.Until && Condition?.HasProblems == true;

    /// <summary>Gets the repeat as a token says it: "once", "3 times", "until pods at least 90 %".</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Gets the condition of an "until", as a sentence with its values as tokens.</summary>
    [ObservableProperty]
    public partial RouteActionViewModel? Condition { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="StepRepeatViewModel"/> class.</summary>
    /// <param name="repeat">How the step is repeated now.</param>
    /// <param name="allowsForever">Whether "for ever" is offered.</param>
    /// <param name="offered">The conditions an "until" can wait on, by node id and as the menu says them.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="card">Makes the sentence of a condition, given its call and what an edit of its values does.</param>
    /// <param name="create">Makes a condition with the values its node starts from, given its node id.</param>
    /// <param name="changed">Told of each change, with the key of a typing run to undo as one.</param>
    public StepRepeatViewModel(StepRepeat repeat, bool allowsForever, IReadOnlyList<(string NodeId, string Name)> offered, Localizer localizer, Func<NodeCall, Action<NodeCall, string?>, RouteActionViewModel?> card, Func<string, NodeCall?> create, Action<StepRepeat, string?> changed)
    {
        AllowsForever = allowsForever;
        _localizer = localizer;
        _card = card;
        _create = create;
        _changed = changed;
        Conditions = [.. offered.Select(item => new RepeatConditionChoiceViewModel(item.NodeId, item.Name, Pick))];
        Show(repeat);
    }

    /// <summary>Shows a repeat that was set elsewhere: the file read again, an undo.</summary>
    /// <param name="repeat">The repeat.</param>
    public void Show(StepRepeat repeat)
    {
        ArgumentNullException.ThrowIfNull(repeat);

        _kind = repeat.Kind;
        _call = repeat.Condition;

        if (repeat.Kind == RepeatKind.Times)
            _count = repeat.Count;

        Condition = _call is null ? null : _card(_call, ConditionEdited);
        Summarize();
        Notify();
    }

    private StepRepeat Current()
    {
        return _kind switch
        {
            RepeatKind.Times => StepRepeat.Times(_count),
            RepeatKind.Forever => StepRepeat.Forever,
            RepeatKind.Until when _call is not null => StepRepeat.Until(_call),
            _ => StepRepeat.Once,
        };
    }

    private void Choose(bool chosen, RepeatKind kind)
    {
        if (!chosen || _kind == kind)
        {
            // A radio button let go of, or pressed again: nothing changes, and the box shows what is true.
            Notify();

            return;
        }

        _kind = kind;

        if (kind == RepeatKind.Until && _call is null && Conditions.Count > 0)
            SetCondition(Conditions[0].NodeId);

        Summarize();
        Notify();
        _changed(Current(), null);
    }

    private void Pick(string node)
    {
        _kind = RepeatKind.Until;
        SetCondition(node);
        Summarize();
        Notify();
        _changed(Current(), null);
    }

    private void SetCondition(string node)
    {
        if (_create(node) is not { } call)
            return;

        _call = call;
        Condition = _card(call, ConditionEdited);
    }

    private void ConditionEdited(NodeCall call, string? run)
    {
        _call = call;
        Condition?.Say();
        Summarize();
        OnPropertyChanged(nameof(HasProblems));
        _changed(Current(), run is null ? null : "repeat-" + run);
    }

    private void Summarize()
    {
        Summary = _kind switch
        {
            RepeatKind.Times => _localizer.Format("RepeatTimes", _count),
            RepeatKind.Forever => _localizer["RepeatForever"],
            RepeatKind.Until => _localizer.Format("RepeatUntil", Lowered(Condition?.Sentence ?? "")),
            _ => _localizer["RepeatOnce"],
        };
    }

    // A sentence that continues a clause starts in lower case, unless its second letter is a capital (an acronym).
    private string Lowered(string sentence)
    {
        return sentence.Length > 1 && char.IsUpper(sentence[0]) && !char.IsUpper(sentence[1]) ? char.ToLower(sentence[0], _localizer.Culture) + sentence[1..] : sentence;
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(IsOnce));
        OnPropertyChanged(nameof(IsTimes));
        OnPropertyChanged(nameof(IsUntil));
        OnPropertyChanged(nameof(IsForever));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasCount));
        OnPropertyChanged(nameof(HasCondition));
        OnPropertyChanged(nameof(HasProblems));
    }
}
