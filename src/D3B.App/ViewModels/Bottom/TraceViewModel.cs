using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Logs;
using D3B.Bot.Activities;
using D3B.Bot.Behaviors;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Bottom;

/// <summary>The trace, in the bottom panel: the decisions of a character's run, what its behavior weighed and chose and how each ended (D327).</summary>
/// <remarks>
/// <para>
/// It reads the bot's trace (<see cref="BotActor.Trace"/>), which the behaviors write, every refresh while it shows, and
/// makes its lines again only once a line was written or the language changed: the trace keeps a thousand lines at most.
/// </para>
/// <para>
/// The character is the run block's (<see cref="Follow"/>), else the first online, until another is chosen in its toolbar;
/// the run is the latest, until an older one is chosen. The newest line comes first.
/// </para>
/// </remarks>
internal sealed partial class TraceViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private GameTextFormatter? _says;
    private CharacterKey? _followed;
    private long _written = -1;
    private CultureInfo? _culture;
    private bool _choosingRun;

    /// <summary>Gets the bots whose runs the trace may show.</summary>
    public ObservableCollection<LogSourceViewModel> Bots { get; } = [];

    /// <summary>Gets the chosen bot's runs whose lines its trace still keeps, the latest first.</summary>
    public ObservableCollection<TraceRunViewModel> Runs { get; } = [];

    /// <summary>Gets the run's lines, the newest first.</summary>
    public ObservableCollection<TraceLineViewModel> Lines { get; } = [];

    /// <summary>Gets the bot whose trace shows.</summary>
    [ObservableProperty]
    public partial LogSourceViewModel? Bot { get; private set; }

    /// <summary>Gets or sets the run whose lines show.</summary>
    [ObservableProperty]
    public partial TraceRunViewModel? Run { get; set; }

    /// <summary>Gets the behavior the run plays, beside the toolbar's choices.</summary>
    [ObservableProperty]
    public partial string Behavior { get; private set; } = "";

    /// <summary>Gets whether there is no line to show.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets what the trace says while it has no line.</summary>
    [ObservableProperty]
    public partial string EmptyText { get; private set; } = "";

    /// <summary>Gets or sets the bot chosen in the toolbar: setting it chooses it, as a click on it does.</summary>
    public LogSourceViewModel? ChosenBot
    {
        get => Bot;
        set
        {
            if (value is not null)
                Choose(value);
        }
    }

    /// <summary>Gets whether a bot is chosen, so that the toolbar shows its runs.</summary>
    public bool HasBot => Bot is not null;

    /// <summary>Gets whether the run's behavior is named.</summary>
    public bool HasBehavior => Behavior.Length > 0;

    /// <summary>Initializes a new instance of the <see cref="TraceViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    public TraceViewModel(Localizer localizer)
    {
        _localizer = localizer;
        EmptyText = localizer["TraceNoBot"];
    }

    /// <summary>Shows the trace of the character the run block chose, until another is chosen in the toolbar.</summary>
    /// <param name="key">The character, or <see langword="null"/> for the first online.</param>
    public void Follow(CharacterKey? key)
    {
        if (_followed == key)
            return;

        _followed = key;

        if (key is { } followed && Find(followed) is { } source)
            Choose(source);
    }

    /// <summary>Reads the bots and their traces again, in the language chosen.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>: what was read stays.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _says is null)
            _says = new GameTextFormatter(_localizer, services.GetService<GameWorld>(), services.GetService<GameTexts>(), services.GetService<GameTables>());

        RefreshBots(services?.GetService<Bots>());

        if (Bot is null && ((_followed is { } key ? Find(key) : null) ?? Bots.FirstOrDefault()) is { } first)
            Choose(first);

        if (Bot?.Bot is not { } bot)
        {
            Show([]);

            return;
        }

        if (bot.Trace.Written == _written && _localizer.Culture.Equals(_culture))
            return;

        _written = bot.Trace.Written;
        _culture = _localizer.Culture;
        Rebuild(bot);
    }

    private static TraceMark MarkOf(TraceEntry entry)
    {
        return entry.Kind switch
        {
            TraceKind.Urgent => TraceMark.Cut,
            TraceKind.Weighed or TraceKind.Rule or TraceKind.Waiting => TraceMark.Always,
            _ => TraceMark.Playing,
        };
    }

    private void RefreshBots(Bots? bots)
    {
        if (bots is null)
            return;

        foreach (var bot in bots.All.OrderBy(bot => bot.Key.AccountId).ThenBy(bot => bot.Key.CharacterId))
        {
            if (Find(bot.Key) is null)
                Bots.Add(new LogSourceViewModel(LogSourceKind.Bot, bot, Choose));
        }

        foreach (var source in Bots)
        {
            var bot = source.Bot!;

            source.Name = bot.State.Name.Length > 0 ? bot.State.Name : _localizer.Format("CharacterNamed", bot.Key.CharacterId);
            source.Tone = bot.IsPaused ? Tone.Warning : bot.IsConnected ? Tone.Success : Tone.Neutral;
        }
    }

    private void Choose(LogSourceViewModel source)
    {
        foreach (var other in Bots)
            other.IsSelected = other == source;

        if (Bot == source)
            return;

        Bot = source;
        Run = null;
        _written = -1;
        OnPropertyChanged(nameof(ChosenBot));
        OnPropertyChanged(nameof(HasBot));

        if (source.Bot is { } bot)
        {
            _written = bot.Trace.Written;
            _culture = _localizer.Culture;
            Rebuild(bot);
        }
    }

    private void Rebuild(BotActor bot)
    {
        var entries = bot.Trace.All();
        var running = bot.Activity is { IsOver: false };
        var latest = Runs.Count > 0 ? Runs[0].Run : (DateTimeOffset?)null;
        var followsLatest = Run is null || Run.Run == latest;
        var runs = entries.GroupBy(entry => entry.Run).OrderByDescending(group => group.Key).ToList();

        _choosingRun = true;

        try
        {
            Runs.Clear();

            for (var index = 0; index < runs.Count; index++)
            {
                var label = index == 0 && running
                    ? _localizer["TraceRunLive"]
                    : _localizer.Format("TraceRunAt", runs[index].Key.ToLocalTime().ToString("t", _localizer.Culture));

                Runs.Add(new TraceRunViewModel(runs[index].Key, runs[index].First().Behavior, label));
            }

            // An older run chosen stays chosen as lines come; the latest is followed as a new run begins.
            Run = followsLatest ? Runs.FirstOrDefault() : Runs.FirstOrDefault(run => run.Run == Run!.Run) ?? Runs.FirstOrDefault();
        }
        finally
        {
            _choosingRun = false;
        }

        Show(Run is { } shown ? [.. entries.Where(entry => entry.Run == shown.Run).Reverse()] : []);
    }

    private void Show(IReadOnlyList<TraceEntry> entries)
    {
        Lines.Clear();

        foreach (var entry in entries)
            Lines.Add(LineOf(entry));

        IsEmpty = Lines.Count is 0;
        EmptyText = Bot is null ? _localizer["TraceNoBot"] : _localizer["TraceEmpty"];
        Behavior = Run?.Behavior ?? "";
        OnPropertyChanged(nameof(HasBehavior));
    }

    private TraceLineViewModel LineOf(TraceEntry entry)
    {
        var culture = _localizer.Culture;
        var time = entry.At.ToLocalTime().ToString("HH:mm:ss", culture);
        var always = _localizer["RailAlways"];
        var state = entry.State ?? _localizer["TraceNoState"];
        var (where, text) = entry.Kind switch
        {
            TraceKind.Entered => (state, _localizer.Format("TraceEntered", state)),
            TraceKind.Waiting => (always, _localizer["TraceWaiting"]),
            TraceKind.Urgent => (state, _localizer.Format("TraceUrgent", entry.Subject ?? "")),
            TraceKind.Weighed => (always, Weighed(entry.Watch)),
            TraceKind.Rule => (always, _localizer.Format("TraceRule", entry.Subject ?? "")),
            _ => (state, entry.Doing is { } doing && _says is not null ? _says.Say(doing) : entry.Node?.In(culture) ?? ""),
        };
        var (outcome, ending) = entry.Outcome switch
        {
            TraceOutcome.Holds => (_localizer["TraceHolds"], TraceEnding.Good),
            TraceOutcome.DoesNotHold => (_localizer["TraceDoesNotHold"], TraceEnding.Neutral),
            TraceOutcome.Played => (_localizer["TracePlayed"], TraceEnding.Good),
            TraceOutcome.Done => (_localizer["TraceDone"], TraceEnding.Good),
            TraceOutcome.Interrupted => (_localizer["TraceInterrupted"], TraceEnding.Bad),
            TraceOutcome.Failed => (_localizer["TraceFailed"], TraceEnding.Bad),
            _ => ("", TraceEnding.Neutral),
        };

        return new TraceLineViewModel(time, where, MarkOf(entry), text, outcome, ending);
    }

    private string Weighed(ActivityWatch? watch)
    {
        if (watch is null)
            return "";

        return _localizer.Format(watch.Gauge is WatchedGauge.Life ? "RailLife" : "RailPods", watch.Threshold, _localizer["Watch" + watch.Then]);
    }

    private LogSourceViewModel? Find(CharacterKey key)
    {
        return Bots.FirstOrDefault(source => source.Bot?.Key == key);
    }

    partial void OnRunChanged(TraceRunViewModel? value)
    {
        if (_choosingRun || Bot?.Bot is not { } bot)
            return;

        Show(value is { } shown ? [.. bot.Trace.All().Where(entry => entry.Run == shown.Run).Reverse()] : []);
    }
}
