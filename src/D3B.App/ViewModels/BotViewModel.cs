using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Characters;
using D3B.Bot.Activities;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors;
using D3B.Bot.Characters;
using D3B.Bot.Fighting;
using D3B.Bot.Harvesting;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Icons;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Storage.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>A bot as the app shows it: its character's card, state and activity, and the commands that start, stop and resume it (D87, D323).</summary>
/// <remarks>
/// It reads the bot's snapshots each time the window refreshes (D9), on the UI's thread: the bot's loop never waits for
/// the app.
/// </remarks>
internal sealed partial class BotViewModel : ObservableObject
{
    private const string NoValue = "—";

    /// <summary>How full a character shows as nearly full, a hint only: each activity says when it unloads (D87).</summary>
    private const double NearlyFull = 0.9;

    /// <summary>Under this share of its life, a card's gauge turns red (D323).</summary>
    private const double LowLife = 50;

    private readonly IServiceProvider _services;
    private readonly Localizer _localizer;
    private readonly GameWorld? _world;
    private readonly GameTexts? _texts;
    private readonly AssetLibrary? _assets;
    private readonly AccountBook? _accounts;
    private readonly ProfileStarter? _starter;
    private readonly NodeCatalog? _nodes;
    private readonly Action<CharacterKey>? _showJournal;
    private readonly Action<CharacterKey>? _showMap;
    private readonly Toasts? _toasts;
    private readonly Action<BotViewModel>? _choose;
    private readonly GameTextFormatter _says;
    private readonly GameIcons? _icons;
    private readonly TimeProvider _time;
    private int _profilesVersion = -1;
    private ActivityRun? _startedRun;
    private ActivityChoiceViewModel? _startedChoice;
    private ActivityReport? _shownReport;
    private CultureInfo? _shownCulture;
    private bool _seen;
    private bool _wasPaused;
    private bool _wasConnected;
    private bool _wasFighting;
    private ActivityRun? _runningSeen;
    private int? _headBreed;
    private bool? _headFemale;

    /// <summary>Gets the bot.</summary>
    public BotActor Bot { get; }

    /// <summary>Gets what the bot can be given: the profiles, by name, then the activities with their defaults (D99).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivities))]
    public partial IReadOnlyList<ActivityChoiceViewModel> Activities { get; private set; } = [];

    /// <summary>Gets whether anything can be given.</summary>
    public bool HasActivities => Activities.Count > 0;

    /// <summary>Gets whether the card opens the bot's journal on the logs page.</summary>
    public bool HasJournal => _showJournal is not null;

    /// <summary>Gets whether the card opens the bot's map on the map page.</summary>
    public bool HasMap => _showMap is not null;

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

    /// <summary>Gets the tone of what the bot is up to, for the dot on its avatar.</summary>
    [ObservableProperty]
    public partial Tone Tone { get; private set; }

    /// <summary>Gets the coordinates of the character's map on the world map.</summary>
    [ObservableProperty]
    public partial string Coordinates { get; private set; } = NoValue;

    /// <summary>Gets the sub-area of the character's map and its cell, or where it is when no coordinates say it.</summary>
    [ObservableProperty]
    public partial string Place { get; private set; } = "";

    /// <summary>Gets the character's map and cell, by their ids.</summary>
    [ObservableProperty]
    public partial string? MapDetail { get; private set; }

    /// <summary>Gets what the character is in the middle of.</summary>
    [ObservableProperty]
    public partial string Doing { get; private set; } = "";

    /// <summary>Gets the character's life, of its most, its regeneration reckoned, once the server said it (D148, D153).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLife), nameof(HasMeters))]
    public partial string? Life { get; private set; }

    /// <summary>Gets the character's life, from 0 to 100 of its most.</summary>
    [ObservableProperty]
    public partial double LifePercent { get; private set; }

    /// <summary>Gets the weight the character carries, of the most it can.</summary>
    [ObservableProperty]
    public partial string Load { get; private set; } = NoValue;

    /// <summary>Gets how full the character is, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double LoadPercent { get; private set; }

    /// <summary>Gets how full the character is, said.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoad), nameof(HasMeters))]
    public partial string LoadPercentText { get; private set; } = "";

    /// <summary>Gets whether the server said the weight the character carries and the most it can.</summary>
    public bool HasLoad => LoadPercentText.Length > 0;

    /// <summary>Gets whether the card shows a gauge, the life's or the pods'.</summary>
    public bool HasMeters => HasLife || HasLoad;

    /// <summary>Gets whether the server said the character's life.</summary>
    public bool HasLife => Life is not null;

    /// <summary>Gets whether the character is nearly full.</summary>
    [ObservableProperty]
    public partial bool IsNearlyFull { get; private set; }

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

    /// <summary>Gets the error that made the activity fail, as it threw it, or why it says it could not go on; empty otherwise.</summary>
    [ObservableProperty]
    public partial string ActivityProblem { get; private set; } = "";

    /// <summary>Gets what the activity says it does, if it reports it (D131).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDoing))]
    public partial string ActivityDoing { get; private set; } = "";

    /// <summary>Gets how far the activity says it is, such as its tour's map.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>Gets how far the activity says it is, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double ProgressPercent { get; private set; }

    /// <summary>Gets how long the activity has run.</summary>
    [ObservableProperty]
    public partial string Elapsed { get; private set; } = "";

    /// <summary>Gets what the activity counts, such as each resource harvested.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCounts))]
    public partial IReadOnlyList<ActivityCountViewModel> Counts { get; private set; } = [];

    /// <summary>Gets what the activity warns of, a line each.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    public partial IReadOnlyList<string> Warnings { get; private set; } = [];

    /// <summary>Gets whether the activity says what it does.</summary>
    public bool HasDoing => ActivityDoing.Length > 0;

    /// <summary>Gets whether the activity says how far it is.</summary>
    public bool HasProgress => ProgressText.Length > 0;

    /// <summary>Gets whether the activity counts something.</summary>
    public bool HasCounts => Counts.Count > 0;

    /// <summary>Gets whether the activity warns of something.</summary>
    public bool HasWarnings => Warnings.Count > 0;

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
    public partial ActivityChoiceViewModel? SelectedActivity { get; set; }

    /// <summary>Gets why the last start failed, if it did.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>Gets or sets whether the run block drives this bot, its card then outlined in the accent (D322, D323).</summary>
    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    /// <summary>Gets or sets the characters' panel, whose menu the card's right click opens.</summary>
    public CharactersViewModel? Panel { get; set; }

    /// <summary>Gets the character's class's head, when the server listed its breed and the pack holds it (D323).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHead))]
    public partial IImage? Head { get; private set; }

    /// <summary>Gets the colour of the character's portrait, its breed's.</summary>
    [ObservableProperty]
    public partial IBrush PortraitColor { get; private set; } = BreedColors.Of(null);

    /// <summary>Gets where the character stands, as its card's ring and tint say.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying), nameof(IsFighting), nameof(IsStopped), nameof(IsHalted))]
    public partial PlayState PlayState { get; private set; }

    /// <summary>Gets what the card says the character does: its activity, a fight, the player's hand, or stopped.</summary>
    [ObservableProperty]
    public partial string CardActivity { get; private set; } = "";

    /// <summary>Gets the glyph of what the character does.</summary>
    [ObservableProperty]
    public partial IImage? ActivityGlyph { get; private set; }

    /// <summary>Gets how far the character is, its tour's map or its fight's round.</summary>
    [ObservableProperty]
    public partial string Step { get; private set; } = "";

    /// <summary>Gets where the character is, in a word: the sub-area, a haven bag, a fight.</summary>
    [ObservableProperty]
    public partial string Where { get; private set; } = "";

    /// <summary>Gets where the character is, its coordinates and sub-area, in the card's tooltip.</summary>
    [ObservableProperty]
    public partial string WhereTip { get; private set; } = "";

    /// <summary>Gets the character's life as a share, said.</summary>
    [ObservableProperty]
    public partial string LifePercentText { get; private set; } = "";

    /// <summary>Gets whether the character's life is under half.</summary>
    [ObservableProperty]
    public partial bool IsLifeLow { get; private set; }

    /// <summary>Gets the life's glyph.</summary>
    public IImage? LifeGlyph { get; }

    /// <summary>Gets the pods' glyph.</summary>
    public IImage? PodsGlyph { get; }

    /// <summary>Gets the place's glyph.</summary>
    public IImage? PlaceGlyph { get; }

    /// <summary>Gets whether the card shows the character's class's head.</summary>
    public bool HasHead => Head is not null;

    /// <summary>Gets whether the character plays, its ring green.</summary>
    public bool IsPlaying => PlayState is PlayState.Playing;

    /// <summary>Gets whether the character fights, its ring red and its card tinted.</summary>
    public bool IsFighting => PlayState is PlayState.Fighting;

    /// <summary>Gets whether the character runs nothing, its ring dim.</summary>
    public bool IsStopped => PlayState is PlayState.Stopped or PlayState.Offline;

    /// <summary>Gets whether the character waits to be resumed, its ring amber.</summary>
    public bool IsHalted => PlayState is PlayState.Paused;

    /// <summary>Initializes a new instance of the <see cref="BotViewModel"/> class.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="services">The host's services: the activities, the profiles, and the pack's world and texts.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="showJournal">Shows the bot's journal on the logs page, or <see langword="null"/> when no page shows it.</param>
    /// <param name="showMap">Shows the bot's map on the map page, or <see langword="null"/> when no page shows it.</param>
    /// <param name="toasts">What the window says in its toasts, of what the bot meets, or <see langword="null"/> to say nothing.</param>
    /// <param name="choose">Chooses the bot for the run block, as a click on its card does, or <see langword="null"/>.</param>
    public BotViewModel(BotActor bot, IServiceProvider services, Localizer localizer, Action<CharacterKey>? showJournal = null, Action<CharacterKey>? showMap = null, Toasts? toasts = null, Action<BotViewModel>? choose = null)
    {
        Bot = bot;
        _toasts = toasts;
        _choose = choose;
        _showJournal = showJournal;
        _showMap = showMap;
        _services = services;
        _localizer = localizer;
        _world = services.GetService<GameWorld>();
        _texts = services.GetService<GameTexts>();
        _assets = services.GetService<AssetLibrary>();
        _accounts = services.GetService<AccountBook>();
        _starter = services.GetService<ProfileStarter>();
        _nodes = services.GetService<NodeCatalog>();
        _says = new GameTextFormatter(localizer, _world, _texts, services.GetService<GameTables>());
        _icons = GameIcons.Of(services.GetService<GameTables>());
        _time = services.GetService<TimeProvider>() ?? TimeProvider.System;
        LifeGlyph = _icons?.Glyph(GameGlyph.Life);
        PodsGlyph = _icons?.Glyph(GameGlyph.Pods);
        PlaceGlyph = _icons?.Glyph(GameGlyph.Place);
        RefreshChoices();
    }

    private static bool Same(ActivityChoiceViewModel choice, ActivityChoiceViewModel other)
    {
        return choice.Profile is { } profile ? profile.Id == other.Profile?.Id : other.Profile is null && choice.Definition?.Id == other.Definition?.Id;
    }

    /// <summary>Gets what a character is in the middle of, as the key of the app's text that says it.</summary>
    /// <param name="state">The character's state.</param>
    /// <returns>The text's key.</returns>
    internal static string DoingOf(CharacterState state)
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
        Initials = AvatarInitials.Of(Name);
        Level = state.Level is { } level ? _localizer.Format("Level", level) : "";
        IsConnected = Bot.IsConnected;
        Connection = _localizer[IsConnected ? "Connected" : "Disconnected"];
        ConnectionTone = IsConnected ? Tone.Success : Tone.Neutral;
        IsPaused = Bot.IsPaused;
        Doing = IsConnected ? _localizer[DoingOf(state)] : "";
        Kamas = _localizer.Format("KamasOf", state.Kamas);

        if ((_assets?.Version ?? 0) != _profilesVersion)
            RefreshChoices();

        foreach (var activity in Activities)
            activity.Refresh(_localizer.Culture);

        RefreshPlace(state);
        RefreshLife(state);
        RefreshLoad(state);
        RefreshActivity();
        RefreshTone();
        RefreshCard(state);
        Announce(state);
    }

    /// <summary>Gets the id of the activity the character plays, or would play: the one started, else its profile's.</summary>
    /// <returns>The activity's id, or <see langword="null"/> when none is known.</returns>
    internal string? ActivityId()
    {
        if (Bot.Activity is { } run && run == _startedRun && _startedChoice is { Definition: { } started })
            return started.Id;

        return (SelectedActivity ?? Activities.FirstOrDefault(choice => choice.Profile?.Id == _accounts?.Find(Bot.Key)?.Profile))?.Definition?.Id;
    }

    /// <summary>Gets the glyph of an activity, as the cards and the character's tab draw it.</summary>
    /// <param name="activityId">The activity's id.</param>
    /// <returns>Its glyph.</returns>
    internal static GameGlyph GlyphOf(string? activityId)
    {
        return activityId switch
        {
            HarvestingModule.ActivityId => GameGlyph.Harvest,
            FightingModule.ActivityId => GameGlyph.Fight,
            _ => GameGlyph.Success,
        };
    }

    private void RefreshCard(CharacterState state)
    {
        var known = _accounts?.Find(Bot.Key);

        // The head is drawn again only when the breed or sex the server listed changes.
        if (known?.BreedId != _headBreed || known?.IsFemale != _headFemale)
        {
            _headBreed = known?.BreedId;
            _headFemale = known?.IsFemale;
            Head = _headBreed is { } breed ? _icons?.Head(breed, _headFemale ?? false) : null;
            PortraitColor = BreedColors.Of(_headBreed);
        }

        PlayState = !IsConnected ? PlayState.Offline
            : state.IsFighting ? PlayState.Fighting
            : IsPaused && IsActivityRunning ? PlayState.Paused
            : IsActivityRunning ? PlayState.Playing
            : IsPaused ? PlayState.Paused
            : PlayState.Stopped;

        (CardActivity, var glyph) = PlayState switch
        {
            PlayState.Fighting => (_localizer["CardFighting"], GameGlyph.Fight),
            PlayState.Paused => (_localizer["CardPaused"], GameGlyph.Pause),
            PlayState.Playing => (ActivityName, GlyphOf(ActivityId())),
            _ => (_localizer["CardStopped"], GameGlyph.None),
        };
        ActivityGlyph = glyph is GameGlyph.None ? null : _icons?.Glyph(glyph);
        Step = PlayState switch
        {
            PlayState.Fighting when state.Fight is { Round: > 0 } fight => _localizer.Format("CardRound", fight.Round),
            // The map it is on, not those it is done with: "3/14" on its third.
            PlayState.Playing when Bot.Activity?.Activity.Report?.Progress is { Total: > 0 } progress => _localizer.Format("CardStep", Math.Min(progress.Done + 1, progress.Total), progress.Total),
            _ => "",
        };

        Where = Place.Length > 0 && Coordinates != NoValue ? PlaceName(state) : Place;
        WhereTip = Coordinates != NoValue ? _localizer.Format("CardWhereTip", Coordinates, Place) : Place;
        LifePercentText = HasLife ? _localizer.Format("LoadPercent", LifePercent / 100) : "";
        IsLifeLow = HasLife && LifePercent < LowLife;
    }

    private string PlaceName(CharacterState state)
    {
        return state.MapId is { } mapId && _world is not null && _world.Atlas.TryGetPosition(mapId, out var position) ? SubAreaName(position.SubAreaId) : Place;
    }

    /// <summary>Chooses what play starts for the session: an activity with its defaults, as the card's list did (D99).</summary>
    /// <param name="activityId">The activity's id.</param>
    public void PreferActivity(string activityId)
    {
        SelectedActivity = Activities.FirstOrDefault(choice => choice.Profile is null && choice.Definition?.Id == activityId) ?? SelectedActivity;
    }

    /// <summary>Chooses what play starts: a profile given to the character, or none.</summary>
    /// <param name="profileId">The profile's id, or <see langword="null"/>.</param>
    public void Prefer(string? profileId)
    {
        SelectedActivity = profileId is null ? null : Activities.FirstOrDefault(choice => choice.Profile?.Id == profileId) ?? SelectedActivity;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (SelectedActivity is not { } choice)
            return;

        if (choice.Profile is { } profile)
            StartProfile(choice, profile);
        else if (choice.Definition is { } definition)
            StartActivity(choice, definition);

        Refresh();
    }

    private void StartProfile(ActivityChoiceViewModel choice, ProfileAsset profile)
    {
        if (_starter is null)
            return;

        var start = _starter.Start(Bot, profile);

        Error = start.Failure switch
        {
            null => null,
            ProfileStartFailure.ProfileMissing => _localizer.Format("ProfileMissing", choice.Name),
            ProfileStartFailure.BehaviorMissing => _localizer.Format("ProfileBehaviorMissing", choice.Name),
            ProfileStartFailure.Invalid when start.Problems.Any(problem => problem.Kind is BehaviorProblemKind.ProfileValueDoesNotRead) => _localizer.Format("ProfileToReview", choice.Name),
            ProfileStartFailure.Invalid => _localizer.Format("ProfileInvalid", choice.Name),
            _ => _localizer.Format("StartFailed", start.Error),
        };

        if (start.Run is { } run)
        {
            _startedRun = run;
            _startedChoice = choice;
        }
    }

    private void StartActivity(ActivityChoiceViewModel choice, ActivityDefinition definition)
    {
        var settings = definition.Settings.CreateDefault();

        // A value the user must give has no default: such an activity runs from a profile.
        if (definition.Settings.Check(settings, _services.GetService<GameReferences>()).Count > 0)
        {
            Error = _localizer.Format("ActivityNeedsProfile", choice.Name);

            return;
        }

        try
        {
            _startedRun = Bot.StartActivity(definition.Create(_services, settings));
            _startedChoice = choice;
            Error = null;
        }
        catch (Exception exception)
        {
            // An activity may fail as it is made; the bot may already run one, or be stopped with the host.
            Error = _localizer.Format("StartFailed", exception.Message);
        }
    }

    private void RefreshChoices()
    {
        var catalog = _services.GetService<ActivityCatalog>();
        var culture = _localizer.Culture;
        var selected = SelectedActivity;
        var choices = new List<ActivityChoiceViewModel>();

        _profilesVersion = _assets?.Version ?? 0;

        foreach (var profile in _assets?.OfType<ProfileAsset>() ?? [])
        {
            var behavior = _assets!.Find<BehaviorAsset>(profile.Behavior);
            var definition = behavior is not null && _nodes is not null ? ProfileForms.ActivityOf(behavior, _nodes) : null;

            choices.Add(new ActivityChoiceViewModel(profile, definition, culture, language => definition?.Name.In(language) ?? behavior?.Title?.In(language) ?? behavior?.Name ?? _localizer.Format("BehaviorMissing", profile.Behavior)));
        }

        foreach (var definition in catalog?.All ?? [])
            choices.Add(new ActivityChoiceViewModel(definition, culture, _ => definition.Settings.IsEmpty ? null : _localizer["DefaultSettings"]));

        var assigned = _accounts?.Find(Bot.Key)?.Profile;

        Activities = choices;
        SelectedActivity = choices.FirstOrDefault(choice => selected is not null && Same(choice, selected))
            ?? choices.FirstOrDefault(choice => assigned is not null && choice.Profile?.Id == assigned)
            ?? (choices.Count == 1 ? choices[0] : null);
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

    [RelayCommand]
    private void Choose()
    {
        _choose?.Invoke(this);
    }

    [RelayCommand]
    private void ShowJournal()
    {
        _showJournal?.Invoke(Bot.Key);
    }

    [RelayCommand]
    private void ShowMap()
    {
        _showMap?.Invoke(Bot.Key);
    }

    private void RefreshPlace(CharacterState state)
    {
        if (state.MapId is not { } mapId)
        {
            Coordinates = NoValue;
            Place = _localizer["NotPlaced"];
            MapDetail = null;

            return;
        }

        var cell = state.Cell?.ToString(_localizer.Culture);

        MapDetail = _localizer.Format("MapCell", mapId, cell ?? NoValue);

        // A haven bag and a fight's map are off the atlas (D83, D88).
        if (state.IsInHavenBag || _world is null || !_world.Atlas.TryGetPosition(mapId, out var position))
        {
            Coordinates = NoValue;
            Place = _localizer[state.IsInHavenBag ? "InHavenBag" : "OffAtlas"];

            return;
        }

        var area = SubAreaName(position.SubAreaId);

        Coordinates = _localizer.Format("Coordinates", position.X, position.Y);
        Place = cell is null ? area : _localizer.Format("AreaCell", area, cell);
    }

    private void RefreshLife(CharacterState state)
    {
        if (Bot.LifeAt(_time.GetUtcNow()) is not { } life || state.MaxLife is not { } most || most <= 0)
        {
            Life = null;
            LifePercent = 0;

            return;
        }

        Life = _localizer.Format("LifeOf", life, most);
        LifePercent = Math.Clamp(life * 100.0 / most, 0, 100);
    }

    private void RefreshLoad(CharacterState state)
    {
        if (state.MaxWeight <= 0)
        {
            Load = NoValue;
            LoadPercent = 0;
            LoadPercentText = "";
            IsNearlyFull = false;

            return;
        }

        Load = _localizer.Format("LoadOf", state.Weight, state.MaxWeight);
        LoadPercent = Math.Clamp(state.Load * 100, 0, 100);
        LoadPercentText = _localizer.Format("LoadPercent", state.Load);
        IsNearlyFull = state.Load >= NearlyFull;
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
            ActivityName = "";
            ActivityText = "";
            ActivityTone = Tone.Neutral;
            ActivityDetail = "";
            ActivityProblem = "";
            RefreshReport(null, null);

            return;
        }

        var (key, tone) = run.IsOver ? OutcomeOf(run.Completion.GetAwaiter().GetResult()) : StatusOf(run.Status);
        var steps = _localizer.Plural("Steps", run.Steps);

        // The name the app started it under, a profile's or an activity's; one started by other code, such as a profile on
        // connection, by the name it was given, else by its own.
        ActivityName = run == _startedRun && _startedChoice is { } choice ? choice.Name : run.Title ?? run.Activity.Name;
        ActivityText = _localizer[key];
        ActivityTone = tone;
        ActivityDetail = run.LastInterruption is { } interruption
            ? _localizer.Format("StepsInterrupted", steps, _localizer[InterruptionOf(interruption)])
            : steps;

        ActivityProblem = !run.IsOver ? ""
            : run.Failure is ActivityException { Reason: { } reason } ? _says.Say(reason)
            : run.IsStillFull ? _localizer["ActivityLeftFull"]
            : run.Failure?.Message ?? "";
        RefreshReport(run, run.Activity.Report);
    }

    private void Announce(CharacterState state)
    {
        var run = Bot.Activity;
        var running = run is { IsOver: false };

        // Only what changed under the app's eyes is said: a bot met for the first time already paused or done says nothing.
        if (_seen && _toasts is { } toasts)
        {
            if (run is { IsOver: true } && run == _runningSeen)
            {
                var outcome = run.Completion.GetAwaiter().GetResult();
                var (key, tone) = run.IsStillFull ? ("ToastStillFull", Tone.Warning)
                    : outcome is ActivityOutcome.Failed ? ("ToastFailed", Tone.Danger)
                    : outcome is ActivityOutcome.Done ? ("ToastDone", Tone.Success)
                    : (null, Tone.Neutral);

                if (key is not null)
                    toasts.Show(new Toast(_localizer.Format(key, Name), tone, _localizer["ShowJournal"], () => _showJournal?.Invoke(Bot.Key)));
            }

            if (IsPaused && !_wasPaused)
                toasts.Show(new Toast(_localizer.Format("ToastPaused", Name), Tone.Warning));

            if (!IsConnected && _wasConnected && (running || _runningSeen is not null))
                toasts.Show(new Toast(_localizer.Format("ToastDisconnected", Name), Tone.Warning, _localizer["ShowJournal"], () => _showJournal?.Invoke(Bot.Key)));

            if (state.IsFighting && !_wasFighting && running)
                toasts.Show(new Toast(_localizer.Format("ToastFight", Name), Tone.Warning, _localizer["ShowMap"], () => _showMap?.Invoke(Bot.Key)));
        }

        _seen = true;
        _wasPaused = IsPaused;
        _wasConnected = IsConnected;
        _wasFighting = state.IsFighting;
        _runningSeen = running ? run : null;
    }

    private void RefreshReport(ActivityRun? run, ActivityReport? report)
    {
        Elapsed = run is null ? "" : ElapsedOf((run.EndedAt ?? _time.GetUtcNow()) - run.StartedAt);

        if (ReferenceEquals(report, _shownReport) && _localizer.Culture.Equals(_shownCulture))
            return;

        _shownReport = report;
        _shownCulture = _localizer.Culture;

        // An activity over says how it ended: what it said it did is no longer true.
        ActivityDoing = report is null || run is { IsOver: true } ? "" : _says.Say(report.Doing);
        ProgressText = report?.Progress is { } progress && run is { IsOver: false } ? _says.Say(progress.Text) : "";
        ProgressPercent = report?.Progress is { Total: > 0 } shown ? Math.Clamp(100.0 * shown.Done / shown.Total, 0, 100) : 0;
        Counts = report is null ? [] : [.. report.Counts.Select(count => new ActivityCountViewModel(_says.Say(count.Text), count.ItemId is not null, count.ItemId is { } item ? _icons?.Item(item) : null))];
        Warnings = report is null || run is { IsOver: true } ? [] : [.. report.Warnings.Select(_says.Say)];
    }

    private string ElapsedOf(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        if (elapsed < TimeSpan.FromMinutes(1))
            return _localizer.Format("ElapsedSeconds", (int)elapsed.TotalSeconds);

        return elapsed < TimeSpan.FromHours(1)
            ? _localizer.Format("ElapsedMinutes", (int)elapsed.TotalMinutes)
            : _localizer.Format("ElapsedHours", (int)elapsed.TotalHours, elapsed.Minutes);
    }

    private void RefreshTone()
    {
        if (IsPaused)
            Tone = Tone.Warning;
        else if (!IsConnected)
            Tone = Tone.Neutral;
        else
            Tone = IsActivityRunning ? ActivityTone : Tone.Info;
    }

    private string SubAreaName(int subAreaId)
    {
        if (_world is not null && _texts is not null && _world.Atlas.TryGetSubArea(subAreaId, out var subArea) && _texts.TryGet(_localizer.GameLanguage, subArea.NameId, out var name))
            return name;

        return _localizer.Format("SubArea", subAreaId);
    }
}
