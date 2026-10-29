using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Movement;
using D3B.Bot.Pages;
using D3B.Bot.Runtime;

namespace D3B.Samples.Hello;

/// <summary>A bot on the plugin's page: who it is, whether it strolls, and what its strolls did.</summary>
internal sealed partial class StrollCardModel : ObservableObject
{
    private readonly StrollJournal _journal;

    /// <summary>Gets the bot.</summary>
    public BotActor Bot { get; }

    /// <summary>Gets the plugin's texts, in the app's language.</summary>
    public PageTexts Texts { get; }

    /// <summary>Gets the character's name.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets the first letters of the character's name.</summary>
    [ObservableProperty]
    public partial string Initials { get; private set; } = "";

    /// <summary>Gets the character's level, when the server gave it.</summary>
    [ObservableProperty]
    public partial string Level { get; private set; } = "";

    /// <summary>Gets whether the bot strolls now.</summary>
    [ObservableProperty]
    public partial bool IsStrolling { get; private set; }

    /// <summary>Gets whether the bot strolls now, or how its last stroll ended, said.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "";

    /// <summary>Gets how many walks of its strolls arrived.</summary>
    [ObservableProperty]
    public partial string Arrivals { get; private set; } = "0";

    /// <summary>Gets what the arrivals count, said.</summary>
    [ObservableProperty]
    public partial string ArrivalsLabel { get; private set; } = "";

    /// <summary>Gets where its last walk went and how it ended.</summary>
    [ObservableProperty]
    public partial string LastWalk { get; private set; } = "";

    /// <summary>Gets whether the bot took a stroll: the one it takes, or the last it took.</summary>
    [ObservableProperty]
    public partial bool HasStroll { get; private set; }

    /// <summary>Gets how far the stroll is, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>Gets how far the stroll it takes is, said.</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>Initializes a new instance of the <see cref="StrollCardModel"/> class.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="journal">What the strolls did.</param>
    /// <param name="texts">The plugin's texts.</param>
    public StrollCardModel(BotActor bot, StrollJournal journal, PageTexts texts)
    {
        Bot = bot;
        _journal = journal;
        Texts = texts;
    }

    private static string InitialsOf(string name)
    {
        return name.Length == 0 ? "?" : name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }

    private static string OutcomeKey(WalkOutcome outcome)
    {
        return outcome switch
        {
            WalkOutcome.Arrived => "OutcomeArrived",
            WalkOutcome.Refused => "OutcomeRefused",
            WalkOutcome.NoPath => "OutcomeNoPath",
            WalkOutcome.NotConfirmed => "OutcomeNotConfirmed",
            _ => "OutcomeNotSent",
        };
    }

    /// <summary>Reads the bot and the journal again, in the app's language.</summary>
    public void Refresh()
    {
        var state = Bot.State;
        var entry = _journal.Of(Bot.Key);
        var run = Bot.Activity;
        var stroll = run?.Activity as Stroll;

        Name = state.Name.Length > 0 ? state.Name : Texts.Format("CharacterNamed", Bot.Key.CharacterId);
        Initials = InitialsOf(Name);
        Level = state.Level is { } level ? Texts.Format("Level", level) : "";
        IsStrolling = stroll is not null && run is { IsOver: false };
        Status = Texts[IsStrolling ? "Strolling" : stroll is not null ? "StrollOver" : "NotStrolling"];
        Arrivals = (entry?.Arrivals ?? 0).ToString(Texts.Culture);
        ArrivalsLabel = Texts.Plural("WalksArrived", entry?.Arrivals ?? 0);
        LastWalk = entry is null ? Texts["NoWalkYet"] : Texts.Format("LastWalk", entry.LastCell, Texts[OutcomeKey(entry.LastOutcome)]);
        HasStroll = stroll is not null;
        Progress = stroll is null ? 0 : 100.0 * stroll.Walked / stroll.Length;
        ProgressText = stroll is null ? "" : Texts.Format("ProgressOf", stroll.Walked, stroll.Length);
    }
}
