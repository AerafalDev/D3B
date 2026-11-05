using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>A reply the picker offers to the question asked now, with what the bots saw follow it (D298, D384).</summary>
internal sealed partial class ReplyChoiceViewModel
{
    private readonly Action<int> _choose;

    /// <summary>Gets the reply's id in the client's tables.</summary>
    public int Reply { get; }

    /// <summary>Gets what the picker calls it: the game sends no text (D383).</summary>
    public string Name { get; }

    /// <summary>Gets what followed it, in words.</summary>
    public string Outcome { get; }

    /// <summary>Initializes a new instance of the <see cref="ReplyChoiceViewModel"/> class.</summary>
    /// <param name="reply">The reply's id.</param>
    /// <param name="name">What the picker calls it.</param>
    /// <param name="outcome">What followed it.</param>
    /// <param name="choose">What choosing it does, given its id.</param>
    public ReplyChoiceViewModel(int reply, string name, string outcome, Action<int> choose)
    {
        Reply = reply;
        Name = name;
        Outcome = outcome;
        _choose = choose;
    }

    [RelayCommand]
    private void Choose()
    {
        _choose(Reply);
    }
}
