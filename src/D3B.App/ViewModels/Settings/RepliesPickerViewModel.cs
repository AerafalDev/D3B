using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Npcs;

namespace D3B.App.ViewModels.Settings;

/// <summary>The dialog that picks the replies given to an NPC, one question after another, among those the bots saw offered: no id is typed (D298, D384).</summary>
/// <remarks>
/// The chain starts from what talking to the NPC led to, then follows each reply to the question the bots saw it lead to. It
/// ends where nothing more is known, or where the reply ended the dialog or moved the character.
/// </remarks>
internal sealed partial class RepliesPickerViewModel : ObservableObject, IDialogViewModel
{
    // How much of a question's text stands in a line saying where a reply leads.
    private const int QuestionLength = 70;

    private readonly Dialogs _dialogs;
    private readonly KnownNpc _known;
    private readonly Localizer _localizer;
    private readonly NpcNames _names;
    private readonly Func<long, string> _place;
    private readonly Action<IReadOnlyList<int>> _picked;
    private readonly List<int> _chain;

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets the text of the button that closes the dialog, keeping the replies.</summary>
    public string DoneText { get; }

    /// <summary>Gets the text of the button that takes the last reply back.</summary>
    public string BackText { get; }

    /// <summary>Gets the text of the button that closes the dialog.</summary>
    public string CancelText { get; }

    /// <summary>Gets what the NPC's question now asked is called, or an empty text.</summary>
    [ObservableProperty]
    public partial string Question { get; private set; } = "";

    /// <summary>Gets what the dialog says of where the chain stands: how it goes on, or why it stops.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "";

    /// <summary>Gets the replies given until now, one line each.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Given { get; private set; } = [];

    /// <summary>Gets the replies the question now asked offers.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChoices))]
    public partial IReadOnlyList<ReplyChoiceViewModel> Choices { get; private set; } = [];

    /// <summary>Gets whether the question now asked offers a reply the bots saw.</summary>
    public bool HasChoices => Choices.Count > 0;

    /// <summary>Gets whether a reply was given, which can be taken back.</summary>
    [ObservableProperty]
    public partial bool CanGoBack { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="RepliesPickerViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="known">The NPC, with what the bots learned of its dialogs.</param>
    /// <param name="chain">The replies given until now.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="names">Reads the questions, the replies and the NPC from the pack's texts.</param>
    /// <param name="place">Names a map, as the atlas knows it.</param>
    /// <param name="picked">What the chain does once the dialog is done.</param>
    public RepliesPickerViewModel(Dialogs dialogs, KnownNpc known, IEnumerable<int> chain, Localizer localizer, NpcNames names, Func<long, string> place, Action<IReadOnlyList<int>> picked)
    {
        _dialogs = dialogs;
        _known = known;
        _localizer = localizer;
        _names = names;
        _place = place;
        _picked = picked;
        _chain = [.. chain];
        Title = localizer.Format("RepliesPickerTitle", names.Npc(known.Npc.NpcId));
        DoneText = localizer["RepliesDone"];
        BackText = localizer["RepliesBack"];
        CancelText = localizer["Cancel"];
        Refresh();
    }

    private int? NextQuestion(int question, int reply)
    {
        var next = _known.Outcomes
            .Where(outcome => outcome.MessageId == question && outcome.ReplyId == reply && outcome.Kind is NpcOutcomeKind.Question)
            .OrderByDescending(outcome => outcome.Count)
            .FirstOrDefault();

        return next is null ? null : (int)next.Target;
    }

    // The question asked after the replies given: none where nothing is known, or where a reply ended the dialog.
    private int? CurrentQuestion()
    {
        var question = NextQuestion(KnownNpcs.Talk, KnownNpcs.Talk);

        foreach (var reply in _chain)
        {
            if (question is not { } asked)
                return null;

            question = NextQuestion(asked, reply);
        }

        return question;
    }

    private string OutcomeOf(int question, int reply)
    {
        var outcomes = _known.Outcomes
            .Where(outcome => outcome.MessageId == question && outcome.ReplyId == reply)
            .OrderByDescending(outcome => outcome.Count)
            .Select(outcome => outcome.Kind switch
            {
                NpcOutcomeKind.Question => _names.TryQuestion((int)outcome.Target, out var text)
                    ? _localizer.Format("RepliesLeadsToText", NpcNames.Clip(text, QuestionLength))
                    : _localizer.Format("RepliesLeadsToQuestion", outcome.Target),
                NpcOutcomeKind.End => _localizer["RepliesEndsDialog"],
                NpcOutcomeKind.MapChange => _localizer.Format("RepliesMovesTo", _place(outcome.Target)),
                _ => _localizer.Format("RepliesOpensExchange", outcome.Target),
            })
            .ToList();

        return outcomes.Count is 0 ? _localizer["RepliesNothingKnown"] : string.Join(" / ", outcomes);
    }

    private void Refresh()
    {
        var given = new List<string>();
        var question = NextQuestion(KnownNpcs.Talk, KnownNpcs.Talk);

        foreach (var (reply, index) in _chain.Select((reply, index) => (reply, index)))
        {
            given.Add(_localizer.Format("RepliesGiven", index + 1, _names.Reply(reply, 60), question is { } asked ? OutcomeOf(asked, reply) : _localizer["RepliesNothingKnown"]));
            question = question is { } before ? NextQuestion(before, reply) : null;
        }

        Given = given;
        CanGoBack = _chain.Count > 0;

        if (question is not { } current)
        {
            Question = "";
            Choices = [];
            Status = _chain.Count > 0 ? _localizer["RepliesChainEnds"] : TalkStatus();

            return;
        }

        Question = _names.Question(current);
        Status = _localizer["RepliesPickOne"];
        Choices =
        [
            .. _known.Questions
                .Where(row => row.MessageId == current)
                .SelectMany(row => row.Replies)
                .Distinct()
                .Select(reply => new ReplyChoiceViewModel(reply, _names.Reply(reply), OutcomeOf(current, reply), Choose)),
        ];
    }

    // What talking to the NPC led to, when it asked no question: the character was moved at once, or nothing was learned.
    private string TalkStatus()
    {
        var talked = _known.Outcomes.Where(outcome => outcome.MessageId == KnownNpcs.Talk && outcome.ReplyId == KnownNpcs.Talk).OrderByDescending(outcome => outcome.Count).FirstOrDefault();

        return talked is { Kind: NpcOutcomeKind.MapChange } ? _localizer.Format("RepliesTalkMoves", _place(talked.Target)) : _localizer["RepliesNothingLearned"];
    }

    private void Choose(int reply)
    {
        _chain.Add(reply);
        Refresh();
    }

    [RelayCommand]
    private void Back()
    {
        if (_chain.Count is 0)
            return;

        _chain.RemoveAt(_chain.Count - 1);
        Refresh();
    }

    [RelayCommand]
    private void Done()
    {
        _picked(_chain);
        _dialogs.Close(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close(this);
    }
}
