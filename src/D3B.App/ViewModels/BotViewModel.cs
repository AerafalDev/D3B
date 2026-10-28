using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Data.Texts;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>A bot as the app shows it: its character's state, its activity, and the commands that start, stop and resume it (D87).</summary>
/// <remarks>
/// It reads the bot's snapshots each time the window refreshes (D9), on the UI's thread: the bot's loop never waits for
/// the app.
/// </remarks>
internal sealed partial class BotViewModel : ObservableObject
{
    private const string NoValue = "—";

    private readonly IServiceProvider _services;
    private readonly Localizer _localizer;
    private readonly GameWorld? _world;
    private readonly GameTexts? _texts;
    private ActivityRun? _startedRun;
    private string? _startedName;

    /// <summary>Gets the bot.</summary>
    public BotActor Bot { get; }

    /// <summary>Gets the activities the bot can be given, as the modules registered them.</summary>
    public IReadOnlyList<ActivityDefinition> Activities { get; }

    /// <summary>Gets whether any activity can be given.</summary>
    public bool HasActivities => Activities.Count > 0;

    /// <summary>Gets the character's name.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets the first letters of the character's name, for its avatar.</summary>
    [ObservableProperty]
    public partial string Initials { get; private set; } = "";

    /// <summary>Gets the character's level, when the server gave it.</summary>
    [ObservableProperty]
    public partial string Level { get; private set; } = "";

    /// <summary>Gets whether a game connection plays the character.</summary>
    [ObservableProperty]
    public partial bool IsConnected { get; private set; }

    /// <summary>Gets whether a game connection plays the character, said.</summary>
    [ObservableProperty]
    public partial string Connection { get; private set; } = "";

    /// <summary>Gets the tone of the connection.</summary>
    [ObservableProperty]
    public partial Tone ConnectionTone { get; private set; }

    /// <summary>Gets whether the player took the character over, so that the bot waits to be resumed (D66).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    public partial bool IsPaused { get; private set; }

    /// <summary>Gets what the bot is up to, in a few words, for the list of bots.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Gets the tone of what the bot is up to, for the list of bots.</summary>
    [ObservableProperty]
    public partial Tone Tone { get; private set; }

    /// <summary>Gets the coordinates of the character's map on the world map.</summary>
    [ObservableProperty]
    public partial string Coordinates { get; private set; } = NoValue;

    /// <summary>Gets the sub-area of the character's map, or where it is when no coordinates say it.</summary>
    [ObservableProperty]
    public partial string Area { get; private set; } = "";

    /// <summary>Gets the character's map and cell.</summary>
    [ObservableProperty]
    public partial string MapDetail { get; private set; } = "";

    /// <summary>Gets what the character is in the middle of.</summary>
    [ObservableProperty]
    public partial string Doing { get; private set; } = "";

    /// <summary>Gets the weight the character carries.</summary>
    [ObservableProperty]
    public partial string Load { get; private set; } = NoValue;

    /// <summary>Gets how full the character is, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double LoadPercent { get; private set; }

    /// <summary>Gets the most the character carries, and how full it is.</summary>
    [ObservableProperty]
    public partial string LoadDetail { get; private set; } = "";

    /// <summary>Gets the kamas the character carries.</summary>
    [ObservableProperty]
    public partial string Kamas { get; private set; } = NoValue;

    /// <summary>Gets whether the bot runs an activity, or ran one.</summary>
    [ObservableProperty]
    public partial bool HasRun { get; private set; }

    /// <summary>Gets the name of the activity the bot runs, or ran last.</summary>
    [ObservableProperty]
    public partial string ActivityName { get; private set; } = "";

    /// <summary>Gets what the activity does, or how it ended.</summary>
    [ObservableProperty]
    public partial string ActivityText { get; private set; } = "";

    /// <summary>Gets the tone of what the activity does, or of how it ended.</summary>
    [ObservableProperty]
    public partial Tone ActivityTone { get; private set; }

    /// <summary>Gets the activity's steps and last interruption.</summary>
    [ObservableProperty]
    public partial string ActivityDetail { get; private set; } = "";

    /// <summary>Gets whether the bot runs an activity that is not over.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    public partial bool IsActivityRunning { get; private set; }

    /// <summary>Gets whether an activity can be chosen and started: none runs, and some exist.</summary>
    [ObservableProperty]
    public partial bool IsChoosing { get; private set; }

    /// <summary>Gets whether no activity runs and none exists to start.</summary>
    [ObservableProperty]
    public partial bool HasNothingToChoose { get; private set; }

    /// <summary>Gets or sets the activity to start.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial ActivityDefinition? SelectedActivity { get; set; }

    /// <summary>Gets why the last start failed, if it did.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="BotViewModel"/> class.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="services">The host's services: the activities, and the pack's world and texts.</param>
    /// <param name="localizer">The app's texts.</param>
    public BotViewModel(BotActor bot, IServiceProvider services, Localizer localizer)
    {
        Bot = bot;
        _services = services;
        _localizer = localizer;
        _world = services.GetService<GameWorld>();
        _texts = services.GetService<GameTexts>();
        Activities = services.GetService<ActivityCatalog>()?.All ?? [];
        SelectedActivity = Activities.Count == 1 ? Activities[0] : null;
    }

    private static string InitialsOf(string name)
    {
        var words = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

        return words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => string.Concat(words[0][0], words[1][0]).ToUpperInvariant(),
        };
    }

    private static string DoingOf(CharacterState state)
    {
        if (state.IsFighting)
            return "Fighting";

        if (state.IsInDialog)
            return "InDialog";

        if (state.IsUsing)
            return "Using";

        return state.IsWalking ? "Walking" : "Idle";
    }

    private static (string Key, Tone Tone) StatusOf(ActivityStatus status)
    {
        return status switch
        {
            ActivityStatus.Waiting => ("ActivityWaiting", Tone.Warning),
            ActivityStatus.Stepping => ("ActivityStepping", Tone.Success),
            ActivityStatus.Unloading => ("ActivityUnloading", Tone.Info),
            ActivityStatus.Recovering => ("ActivityRecovering", Tone.Info),
            _ => ("ActivityEnding", Tone.Neutral),
        };
    }

    private static (string Key, Tone Tone) OutcomeOf(ActivityOutcome outcome)
    {
        return outcome switch
        {
            ActivityOutcome.Done => ("ActivityDone", Tone.Success),
            ActivityOutcome.Failed => ("ActivityFailed", Tone.Danger),
            _ => ("ActivityStopped", Tone.Neutral),
        };
    }

    private static string InterruptionOf(Interruption interruption)
    {
        return interruption switch
        {
            Interruption.Player => "InterruptedByPlayer",
            Interruption.Disconnected => "InterruptedByDisconnection",
            Interruption.Fight => "InterruptedByFight",
            _ => "InterruptedByStop",
        };
    }

    /// <summary>Reads the bot again, in the language chosen.</summary>
    public void Refresh()
    {
        var state = Bot.State;

        Name = state.Name.Length > 0 ? state.Name : _localizer.Format("CharacterNamed", Bot.Key.CharacterId);
        Initials = InitialsOf(Name);
        Level = state.Level is { } level ? _localizer.Format("Level", level) : "";
        IsConnected = Bot.IsConnected;
        Connection = _localizer[IsConnected ? "Connected" : "Disconnected"];
        ConnectionTone = IsConnected ? Tone.Success : Tone.Neutral;
        IsPaused = Bot.IsPaused;
        Doing = _localizer[DoingOf(state)];
        Kamas = _localizer.Format("Number", state.Kamas);
        RefreshPlace(state);
        RefreshLoad(state);
        RefreshActivity();
        RefreshSummary();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (SelectedActivity is not { } definition)
            return;

        try
        {
            _startedRun = Bot.StartActivity(definition.Create(_services));
            _startedName = definition.Name;
            Error = null;
        }
        catch (Exception exception)
        {
            // An activity a plugin wrote may fail as it is made; the bot may already run one, or be stopped with the host.
            Error = _localizer.Format("StartFailed", exception.Message);
        }

        Refresh();
    }

    private bool CanStart()
    {
        return SelectedActivity is not null && !IsActivityRunning;
    }

    [RelayCommand(CanExecute = nameof(IsActivityRunning))]
    private void Stop()
    {
        Bot.Activity?.Stop();
    }

    [RelayCommand(CanExecute = nameof(IsPaused))]
    private void Resume()
    {
        Bot.Resume();
    }

    private void RefreshPlace(CharacterState state)
    {
        if (state.MapId is not { } mapId)
        {
            Coordinates = NoValue;
            Area = _localizer["NotPlaced"];
            MapDetail = "";

            return;
        }

        MapDetail = _localizer.Format("MapCell", mapId, state.Cell?.ToString(_localizer.Culture) ?? NoValue);

        // A haven bag and a fight's map are off the atlas (D83, D88).
        if (state.IsInHavenBag || _world is null || !_world.Atlas.TryGetPosition(mapId, out var position))
        {
            Coordinates = NoValue;
            Area = _localizer[state.IsInHavenBag ? "InHavenBag" : "OffAtlas"];

            return;
        }

        Coordinates = _localizer.Format("Coordinates", position.X, position.Y);
        Area = SubAreaName(position.SubAreaId);
    }

    private void RefreshLoad(CharacterState state)
    {
        if (state.MaxWeight <= 0)
        {
            Load = NoValue;
            LoadPercent = 0;
            LoadDetail = "";

            return;
        }

        Load = _localizer.Format("Number", state.Weight);
        LoadPercent = Math.Clamp(state.Load * 100, 0, 100);
        LoadDetail = _localizer.Format("LoadDetail", state.MaxWeight, state.Load);
    }

    private void RefreshActivity()
    {
        var run = Bot.Activity;

        IsActivityRunning = run is { IsOver: false };
        IsChoosing = !IsActivityRunning && HasActivities;
        HasNothingToChoose = !IsActivityRunning && !HasActivities;
        HasRun = run is not null;

        if (run is null)
        {
            ActivityName = _localizer["NoActivity"];
            ActivityText = "";
            ActivityTone = Tone.Neutral;
            ActivityDetail = _localizer["NoActivityDetail"];

            return;
        }

        var (key, tone) = run.IsOver ? OutcomeOf(run.Completion.GetAwaiter().GetResult()) : StatusOf(run.Status);
        var steps = _localizer.Plural("Steps", run.Steps);

        // The name the app started it under; an activity started by other code goes by its own.
        ActivityName = run == _startedRun && _startedName is { } name ? name : run.Activity.Name;
        ActivityText = _localizer[key];
        ActivityTone = tone;
        ActivityDetail = run.LastInterruption is { } interruption
            ? _localizer.Format("StepsInterrupted", steps, _localizer[InterruptionOf(interruption)])
            : steps;
    }

    private void RefreshSummary()
    {
        if (IsPaused)
        {
            Summary = _localizer["Paused"];
            Tone = Tone.Warning;
        }
        else if (!IsConnected)
        {
            Summary = Connection;
            Tone = Tone.Neutral;
        }
        else if (IsActivityRunning)
        {
            Summary = _localizer.Format("Doing", ActivityName, ActivityText);
            Tone = ActivityTone;
        }
        else
        {
            Summary = _localizer["NoActivity"];
            Tone = Tone.Info;
        }
    }

    private string SubAreaName(int subAreaId)
    {
        if (_world is not null && _texts is not null && _world.Atlas.TryGetSubArea(subAreaId, out var subArea) && _texts.TryGet(_localizer.GameLanguage, subArea.NameId, out var name))
            return name;

        return _localizer.Format("SubArea", subAreaId);
    }
}
