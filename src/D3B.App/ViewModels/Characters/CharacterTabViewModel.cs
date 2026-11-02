using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.App.ViewModels.Logs;
using D3B.App.ViewModels.Maps;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Icons;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Storage.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Characters;

/// <summary>A character's tab: its map live, and a column of what it does, its behavior, its session and its latest decisions (D325).</summary>
/// <remarks>
/// <para>
/// The column reads the character's bot every 250 ms while the tab shows. What it does now is large, with the tour's
/// progress, the time spent and when it should end at the pace kept so far. The rail lists the layer Always, each threshold
/// the activity watches with a gauge of the value against it (D325, the owner's choice), then the behavior's states, the one
/// playing lit. The session counts what the run brought; the thread, the journal's latest entries but the walks.
/// </para>
/// <para>Texts short and plain, the owner's ask: "Fin estimée 16:07", "Pods ≥ 90 % → Banque".</para>
/// </remarks>
internal sealed partial class CharacterTabViewModel : ObservableObject
{
    private const int DecisionsShown = 4;

    private readonly Localizer _localizer;
    private readonly BotsViewModel _bots;
    private readonly Action<string> _openProfile;
    private readonly Action<CharacterKey> _showJournal;
    private readonly Dictionary<int, (int Level, long Experience)> _jobsAtStart = [];
    private AccountBook? _book;
    private GameTables? _tables;
    private GameTexts? _texts;
    private GameWorld? _world;
    private GameIcons? _icons;
    private JournalLines? _lines;
    private TimeProvider _time = TimeProvider.System;
    private ActivityRun? _run;
    private long _kamasAtStart;
    private long _journalSeen = -1;
    private CultureInfo? _culture;

    /// <summary>Gets the character.</summary>
    public CharacterKey Key { get; }

    /// <summary>Gets the character's map.</summary>
    public MapViewModel Map { get; }

    /// <summary>Gets the character's name, its tab's title.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets where the character stands, which tints what it does now.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying), nameof(IsPaused), nameof(IsFighting))]
    public partial PlayState PlayState { get; private set; } = PlayState.Offline;

    /// <summary>Gets what the character does now, in a word.</summary>
    [ObservableProperty]
    public partial string NowTitle { get; private set; } = "";

    /// <summary>Gets what the character does now, said, and how far it is.</summary>
    [ObservableProperty]
    public partial string NowDetail { get; private set; } = "";

    /// <summary>Gets the place's glyph, beside the map's coordinates.</summary>
    [ObservableProperty]
    public partial IImage? PlaceGlyph { get; private set; }

    /// <summary>Gets the time's glyph, beside how long the activity has run.</summary>
    [ObservableProperty]
    public partial IImage? TimeGlyph { get; private set; }

    /// <summary>Gets the glyph of what the character does.</summary>
    [ObservableProperty]
    public partial IImage? NowGlyph { get; private set; }

    /// <summary>Gets how far the activity is, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>Gets whether the activity says how far it is.</summary>
    [ObservableProperty]
    public partial bool HasProgress { get; private set; }

    /// <summary>Gets the tour's maps done and their number, beside its grid on the map.</summary>
    [ObservableProperty]
    public partial string TourText { get; private set; } = "";

    /// <summary>Gets how long the activity has run.</summary>
    [ObservableProperty]
    public partial string Elapsed { get; private set; } = "";

    /// <summary>Gets when the activity should end at the pace kept so far, or nothing.</summary>
    [ObservableProperty]
    public partial string EndEstimate { get; private set; } = "";

    /// <summary>Gets what went wrong: the error the activity failed with, or why it could not start.</summary>
    [ObservableProperty]
    public partial string Problem { get; private set; } = "";

    /// <summary>Gets what the activity warns of, a line each.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Warnings { get; private set; } = [];

    /// <summary>Gets the profile the character plays, or what the app says for none.</summary>
    [ObservableProperty]
    public partial string ProfileName { get; private set; } = "";

    /// <summary>Gets the rail: the layer Always, its rules, then the behavior's states.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRail))]
    public partial IReadOnlyList<RailRowViewModel> Rail { get; private set; } = [];

    /// <summary>Gets when the session began.</summary>
    [ObservableProperty]
    public partial string Since { get; private set; } = "";

    /// <summary>Gets what the session brought, a tile per resource.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SessionTileViewModel> Tiles { get; private set; } = [];

    /// <summary>Gets the profession the session raised, and its level.</summary>
    [ObservableProperty]
    public partial string Profession { get; private set; } = "";

    /// <summary>Gets the levels gained and the share of the next level reached.</summary>
    [ObservableProperty]
    public partial string ProfessionGain { get; private set; } = "";

    /// <summary>Gets the share of the profession's next level reached, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double ProfessionProgress { get; private set; }

    /// <summary>Gets the session's fights, deposits and kamas, a fact each.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Facts { get; private set; } = [];

    /// <summary>Gets the latest decisions, the latest first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DecisionViewModel> Decisions { get; private set; } = [];

    /// <summary>Gets whether the character plays, what it does now tinted green.</summary>
    public bool IsPlaying => PlayState is PlayState.Playing;

    /// <summary>Gets whether the character waits to be resumed, amber.</summary>
    public bool IsPaused => PlayState is PlayState.Paused;

    /// <summary>Gets whether the character fights, red.</summary>
    public bool IsFighting => PlayState is PlayState.Fighting;

    /// <summary>Gets whether the rail lists something: nothing plays while it is empty.</summary>
    public bool HasRail => Rail.Count > 0;

    /// <summary>Gets whether the session counted a resource.</summary>
    public bool HasTiles => Tiles.Count > 0;

    /// <summary>Gets whether the session raised a profession.</summary>
    public bool HasProfession => Profession.Length > 0;

    /// <summary>Gets whether the activity ran, so that the session has something to say.</summary>
    public bool HasSession => _run is not null;

    /// <summary>Gets whether something went wrong.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>Gets whether the activity warns of something.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>Gets whether the thread holds a decision.</summary>
    public bool HasDecisions => Decisions.Count > 0;

    /// <summary>Gets whether the character plays a profile, which its button opens.</summary>
    public bool HasProfile => ProfileId is not null;

    private string? ProfileId { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CharacterTabViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="key">The character.</param>
    /// <param name="bots">The bots, the character's among them once the host made it.</param>
    /// <param name="openProfile">Opens a profile, by its id.</param>
    /// <param name="showJournal">Shows a character's journal, for the whole trace until 7c4's.</param>
    public CharacterTabViewModel(Localizer localizer, CharacterKey key, BotsViewModel bots, Action<string> openProfile, Action<CharacterKey> showJournal)
    {
        _localizer = localizer;
        _bots = bots;
        _openProfile = openProfile;
        _showJournal = showJournal;
        Key = key;
        Map = new MapViewModel(localizer, key);
    }

    /// <summary>Reads the character again, in the language chosen.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>: what was shown stays.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _tables is null)
        {
            _book = services.GetService<AccountBook>();
            _tables = services.GetService<GameTables>();
            _texts = services.GetService<GameTexts>();
            _world = services.GetService<GameWorld>();
            _icons = GameIcons.Of(_tables);
            PlaceGlyph = _icons?.Glyph(GameGlyph.Place);
            TimeGlyph = _icons?.Glyph(GameGlyph.Time);
            _lines = new JournalLines(_localizer, _world, _texts, _tables);
            _time = services.GetService<TimeProvider>() ?? TimeProvider.System;
        }

        Map.Refresh(services);

        var bot = _bots.Items.FirstOrDefault(item => item.Bot.Key == Key);
        var known = _book?.Find(Key);

        Name = bot?.Name ?? known?.Name ?? _localizer.Format("CharacterNamed", Key.CharacterId);
        ProfileId = known?.Profile;
        ProfileName = bot?.SelectedActivity?.Name ?? (ProfileId is null ? _localizer["NoProfile"] : ProfileId);
        OnPropertyChanged(nameof(HasProfile));

        if (bot is null)
        {
            PlayState = PlayState.Offline;
            NowTitle = _localizer["Disconnected"];
            NowDetail = known?.LastPlayed is { } last ? Ago.Of(_localizer, "Played", last, _time.GetUtcNow()) : "";

            return;
        }

        RefreshNow(bot);
        RefreshRail(bot);
        RefreshSession(bot);
        RefreshDecisions(bot.Bot);
        _culture = _localizer.Culture;
    }

    [RelayCommand]
    private void OpenProfile()
    {
        if (ProfileId is { } id)
            _openProfile(id);
    }

    [RelayCommand]
    private void ShowTrace()
    {
        _showJournal(Key);
    }

    private void RefreshNow(BotViewModel bot)
    {
        var run = bot.Bot.Activity;
        var report = run is { IsOver: false } ? run.Activity.Report : null;

        PlayState = bot.PlayState;
        (NowTitle, var glyph) = bot.PlayState switch
        {
            PlayState.Fighting => (_localizer["CardFighting"], GameGlyph.Fight),
            PlayState.Paused => (_localizer["CardPaused"], GameGlyph.Pause),
            PlayState.Playing => (bot.ActivityName, BotViewModel.GlyphOf(bot.ActivityId())),
            PlayState.Offline => (_localizer["Disconnected"], GameGlyph.Character),
            _ => (run is { IsOver: true } ? bot.ActivityText : _localizer["CardStopped"], GameGlyph.Character),
        };
        NowGlyph = _icons?.Glyph(glyph);
        NowDetail = string.Join(" · ", new[] { bot.ActivityDoing, bot.ProgressText }.Where(text => text.Length > 0)) is { Length: > 0 } doing ? doing : bot.WhereTip;
        HasProgress = report?.Progress is { Total: > 0 };
        TourText = report?.Progress is { Total: > 0 } tour ? _localizer.Format("TourCount", tour.Done, tour.Total) : "";
        Progress = bot.ProgressPercent;
        Elapsed = bot.Elapsed;
        EndEstimate = EndOf(run, report?.Progress);
        Problem = bot.Error ?? bot.ActivityProblem;
        Warnings = bot.Warnings;
        OnPropertyChanged(nameof(HasProblem));
        OnPropertyChanged(nameof(HasWarnings));
    }

    private string EndOf(ActivityRun? run, ActivityProgress? progress)
    {
        if (run is not { IsOver: false } || progress is not { Done: > 0 } shown || shown.Done >= shown.Total)
            return "";

        // At the pace kept so far: the tour's maps left take as long as those done, a hint only.
        var now = _time.GetUtcNow();
        var spent = now - run.StartedAt;
        var end = now + (spent * (shown.Total - shown.Done) / shown.Done);

        return _localizer.Format("EndEstimate", end.ToLocalTime().DateTime);
    }

    private void RefreshRail(BotViewModel bot)
    {
        var run = bot.Bot.Activity;
        var activity = run is { IsOver: false } ? run.Activity : null;
        var state = bot.Bot.State;
        var rows = new List<RailRowViewModel>();
        var watches = activity?.Watches ?? [];
        var outline = activity?.Outline;

        if (watches.Length > 0 || outline is { Always.Length: > 0 })
            rows.Add(new RailRowViewModel(RailRowKind.Layer, _localizer["RailAlways"]));

        foreach (var watch in watches)
        {
            var isLife = watch.Gauge is WatchedGauge.Life;
            var value = isLife ? (bot.HasLife ? bot.LifePercent : (double?)null) : state.MaxWeight > 0 ? bot.LoadPercent : null;

            rows.Add(new RailRowViewModel(RailRowKind.Rule, _localizer.Format(isLife ? "RailLife" : "RailPods", watch.Threshold, _localizer["Watch" + watch.Then]))
            {
                Gauge = value,
                Threshold = watch.Threshold,
                IsLife = isLife,
                GaugeTip = value is { } shown ? _localizer.Format("RailGaugeTip", _localizer[isLife ? "Life" : "Pods"], shown / 100, watch.Threshold / 100.0) : "",
            });
        }

        foreach (var always in outline?.Always ?? [])
            rows.Add(new RailRowViewModel(RailRowKind.Rule, always.Name, always.Depth) { IsActive = always.Id == outline!.Playing, ActiveText = _localizer["RailActive"] });

        if (outline is { States.Length: > 0 })
        {
            foreach (var outlined in outline.States)
                rows.Add(new RailRowViewModel(RailRowKind.State, outlined.Name, outlined.Depth) { IsActive = outlined.Id == outline.Playing, ActiveText = _localizer["RailActive"] });
        }
        else if (activity is not null)
        {
            rows.Add(new RailRowViewModel(RailRowKind.State, bot.ActivityName) { IsActive = true, ActiveText = _localizer["RailActive"] });
        }

        if (!rows.SequenceEqual(Rail))
            Rail = rows;
    }

    private void RefreshSession(BotViewModel bot)
    {
        var run = bot.Bot.Activity;
        var state = bot.Bot.State;

        if (run != _run)
        {
            _run = run;
            _kamasAtStart = state.Kamas;
            _jobsAtStart.Clear();

            foreach (var job in state.Jobs.Values)
                _jobsAtStart[job.JobId] = (job.Level, job.Experience);

            OnPropertyChanged(nameof(HasSession));
        }

        if (run is null)
            return;

        var statistics = run.Activity.Statistics;
        var hours = Math.Max(((run.EndedAt ?? _time.GetUtcNow()) - run.StartedAt).TotalHours, 1.0 / 60);

        Since = _localizer.Format("SessionSince", run.StartedAt.ToLocalTime().DateTime);
        // What the activity wants counts from zero, as its report lists it; then what it brought besides, such as loot.
        var wanted = run.Activity.Report?.Counts.Select(count => count.ItemId).OfType<int>().ToList() ?? [];
        var items = wanted.Concat((statistics?.Items ?? []).OrderByDescending(item => item.Value).Select(item => item.Key)).Distinct();

        Tiles = [.. items.Select(item => (Id: item, Count: statistics?.Items.GetValueOrDefault(item) ?? 0)).Select(item => new SessionTileViewModel(_icons?.Item(item.Id), item.Count.ToString("N0", _localizer.Culture), _localizer.Format("SessionTileDetail", ItemName(item.Id), item.Count / hours)))];
        OnPropertyChanged(nameof(HasTiles));
        RefreshProfession(state);

        var fights = statistics?.Fights.Values.Sum() ?? 0;
        var kamas = state.Kamas - _kamasAtStart;

        Facts =
        [
            _localizer.Format("SessionFights", fights),
            _localizer.Format("SessionDeposits", statistics?.Banks ?? 0),
            _localizer.Format(kamas >= 0 ? "SessionKamasGained" : "SessionKamasSpent", Math.Abs(kamas)),
        ];
    }

    private void RefreshProfession(CharacterState state)
    {
        // The profession the session raised most, by the experience it brought.
        var raised = state.Jobs.Values
            .Where(job => _jobsAtStart.TryGetValue(job.JobId, out var start) && job.Experience > start.Experience)
            .OrderByDescending(job => job.Experience - _jobsAtStart[job.JobId].Experience)
            .FirstOrDefault();

        if (raised is null || _tables is null || !_tables.Jobs.TryGetJob(raised.JobId, out var job))
        {
            Profession = "";
            OnPropertyChanged(nameof(HasProfession));

            return;
        }

        var name = _texts is not null && _texts.TryGet(_localizer.GameLanguage, job.NameId, out var text) ? text : raised.JobId.ToString(_localizer.Culture);
        var span = raised.NextLevelFloor - raised.LevelFloor;

        ProfessionProgress = span > 0 ? Math.Clamp(100.0 * (raised.Experience - raised.LevelFloor) / span, 0, 100) : 100;
        Profession = _localizer.Format("SessionProfession", name, raised.Level);
        ProfessionGain = _localizer.Format("SessionProfessionGain", raised.Level - _jobsAtStart[raised.JobId].Level, ProfessionProgress / 100);
        OnPropertyChanged(nameof(HasProfession));
    }

    private string ItemName(int itemId)
    {
        return _tables is not null && _texts is not null && _tables.Items.TryGetItem(itemId, out var item) && _texts.TryGet(_localizer.GameLanguage, item.NameId, out var name)
            ? name
            : _localizer.Format("ItemNamed", itemId);
    }

    private void RefreshDecisions(BotActor bot)
    {
        if (_lines is null || (bot.Journal.Written == _journalSeen && _localizer.Culture.Equals(_culture)))
            return;

        _journalSeen = bot.Journal.Written;

        // The walks are steps of a decision, not one: a tour's would fill the thread.
        Decisions =
        [
            .. bot.Journal.All()
                .Where(entry => entry is not WalkEntry && entry.Depth == 0)
                .TakeLast(DecisionsShown)
                .Reverse()
                .Select(entry => _lines.Of(entry, null))
                .Select(line => new DecisionViewModel(line.Text, line.Detail, line.At.ToLocalTime().ToString("t", _localizer.Culture), line.Tone)),
        ];
        OnPropertyChanged(nameof(HasDecisions));
    }
}
