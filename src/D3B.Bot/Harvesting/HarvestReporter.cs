using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;

namespace D3B.Bot.Harvesting;

/// <summary>What a harvesting activity says of itself: its card, with where it stands in the tour, what it harvested and what it warns of, and its statistics (D127 to D132, D146).</summary>
/// <remarks>The activity tells it what happened, and it keeps the figures; the card and the statistics are read from other threads.</remarks>
internal sealed class HarvestReporter
{
    private readonly HarvestSettings _settings;
    private readonly HarvestUpkeep _upkeep;
    private readonly Dictionary<int, long> _harvested = [];
    private readonly Dictionary<FightKind, int> _fights = [];
    private ActivityReport? _report = new() { Doing = GameText.Of(HarvestTexts.Text("DoingStart")) };
    private ActivityStatistics? _statistics;
    private DateTimeOffset? _startedAt;
    private ImmutableSortedDictionary<int, CharacterJob>? _startJobs;
    private BotContext? _context;
    private int _banks;

    /// <summary>Gets the card now.</summary>
    public ActivityReport? Report => Volatile.Read(ref _report);

    /// <summary>Gets the statistics now.</summary>
    public ActivityStatistics? Statistics => Volatile.Read(ref _statistics);

    /// <summary>Gets or sets the warning the guild chest gave, by its text's key: what it refused, or that none was reached (D147).</summary>
    public string? ChestWarning { get; set; }

    /// <summary>Initializes a new instance of the <see cref="HarvestReporter"/> class.</summary>
    /// <param name="settings">What the profile harvests.</param>
    /// <param name="upkeep">The upkeep, whose bags and food count in the figures.</param>
    public HarvestReporter(HarvestSettings settings, HarvestUpkeep upkeep)
    {
        _settings = settings;
        _upkeep = upkeep;
    }

    /// <summary>Remembers the context the activity runs in, and, the first time, when and with which professions' experience it began.</summary>
    /// <param name="context">The bot's context.</param>
    public void Begin(BotContext context)
    {
        _context = context;
        _startedAt ??= context.Time.GetUtcNow();
        _startJobs ??= context.State.Jobs;
    }

    /// <summary>Counts what a harvest brought.</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="gained">How many of it the character carries more.</param>
    public void Harvested(int itemId, long gained)
    {
        _harvested[itemId] = _harvested.GetValueOrDefault(itemId) + gained;
    }

    /// <summary>Counts a banking done.</summary>
    public void Banked()
    {
        _banks++;
    }

    /// <summary>Counts a fight that cut the activity.</summary>
    /// <param name="kind">Which kind.</param>
    public void Fought(FightKind kind)
    {
        _fights[kind] = _fights.GetValueOrDefault(kind) + 1;
    }

    /// <summary>Says on the card why the activity was interrupted, with the figures up to date.</summary>
    /// <param name="zone">The zone the activity harvests, if it has one yet.</param>
    /// <param name="doing">What the card says.</param>
    public void Interrupted(HarvestZone? zone, GameText doing)
    {
        if (Report is not { } report)
            return;

        Tally();

        var counts = zone is not null ? CountsOf(zone) : report.Counts;

        Volatile.Write(ref _report, report with { Doing = doing, Counts = counts });
    }

    /// <summary>Says on the card what the activity does and where it stands.</summary>
    /// <param name="current">The zone of the rotation it harvests, if it has one yet.</param>
    /// <param name="state">The character's state.</param>
    /// <param name="doing">What it does.</param>
    /// <param name="target">The element it harvests, if one.</param>
    /// <param name="next">The map it goes to, if it travels.</param>
    public void Publish(HarvestRotationZone? current, CharacterState state, GameText doing, int? target = null, long? next = null)
    {
        if (current is null)
        {
            Volatile.Write(ref _report, new ActivityReport { Doing = doing });

            return;
        }

        var zone = current.Zone;
        var tour = current.Tour;

        Tally();

        var total = tour.Total(zone);
        var growing = tour.Growing(zone);

        // The place the activity chose names itself first, the 5th value (D166).
        var automatic = _settings.Place is HarvestPlace.Automatic && zone.SubAreas.Count > 0;
        var values = new[]
        {
            GameTextValue.Number(tour.Round),
            GameTextValue.Number(Math.Min(tour.Visited + 1, total)),
            GameTextValue.Number(total),
            GameTextValue.Number(growing),
            GameTextValue.SubArea(automatic ? zone.SubAreas[0] : 0),
        };
        var key = (growing > 0, automatic) switch
        {
            (true, true) => "ProgressAtGrowing",
            (true, false) => "ProgressGrowing",
            (false, true) => "ProgressAt",
            _ => "Progress",
        };
        var progress = new ActivityProgress(GameText.Of(HarvestTexts.Text(key), values), tour.Visited, total);

        Volatile.Write(ref _report, new ActivityReport
        {
            Doing = doing,
            Progress = progress,
            Counts = CountsOf(zone),
            Warnings = WarningsOf(zone, state),
            Focus = new ActivityFocus
            {
                MapId = state.MapId,
                TargetElementId = target,
                WantedElementIds = [.. state.Elements.Values.Where(element => element.EnabledSkills.Any(skill => zone.Targets.ContainsKey(skill.SkillId))).Select(element => element.ElementId)],
                NextMapId = next,
                Zone = tour.Maps(zone),
            },
        });
    }

    private void Tally()
    {
        Volatile.Write(ref _statistics, new ActivityStatistics
        {
            Items = _harvested.ToImmutableDictionary(),
            Banks = _banks,
            Fights = _fights.ToImmutableDictionary(),
        });
    }

    private ImmutableArray<ActivityCount> CountsOf(HarvestZone zone)
    {
        List<int> items = _settings.Mode is HarvestMode.ByProfession
            ? [.. _harvested.Keys.Order()]
            : [.. _settings.Resources.Where(zone.Wanted.Contains).Concat(_harvested.Keys.Where(item => !_settings.Resources.Contains(item))).Distinct()];

        // A rate over less than a minute says little: shown once the activity ran that long (D146).
        var hours = _context is { } context && _startedAt is { } started ? (context.Time.GetUtcNow() - started).TotalHours : 0;
        var counts = items.Select(item => new ActivityCount(
            hours >= 1.0 / 60
                ? GameText.Of(HarvestTexts.Text("CountItemRate"), GameTextValue.Item(item), GameTextValue.Number(_harvested.GetValueOrDefault(item)), GameTextValue.Number((long)Math.Round(_harvested.GetValueOrDefault(item) / hours)))
                : GameText.Of(HarvestTexts.Text("CountItem"), GameTextValue.Item(item), GameTextValue.Number(_harvested.GetValueOrDefault(item))),
            item)).ToList();

        if (_context?.State is { } now && _startJobs is { } before)
        {
            foreach (var job in now.Jobs.Values)
            {
                var gained = job.Experience - (before.TryGetValue(job.JobId, out var then) ? then.Experience : job.Experience);

                if (gained > 0)
                    counts.Add(new ActivityCount(GameText.Of(HarvestTexts.Text("CountExperience"), GameTextValue.Job(job.JobId), GameTextValue.Number(gained))));
            }
        }

        if (_banks > 0)
            counts.Add(new ActivityCount(GameText.Counted(HarvestTexts.Text("CountBanksOne"), HarvestTexts.Text("CountBanksMany"), _banks)));

        if (_upkeep.Bags > 0)
            counts.Add(new ActivityCount(GameText.Counted(HarvestTexts.Text("CountBagsOne"), HarvestTexts.Text("CountBagsMany"), _upkeep.Bags)));

        if (_upkeep.Eaten > 0)
            counts.Add(new ActivityCount(GameText.Counted(HarvestTexts.Text("CountEatenOne"), HarvestTexts.Text("CountEatenMany"), _upkeep.Eaten)));

        var fights = _fights.Values.Sum();

        if (fights > 0)
            counts.Add(new ActivityCount(GameText.Counted(HarvestTexts.Text("CountFightsOne"), HarvestTexts.Text("CountFightsMany"), fights, GameTextValue.Number(_fights.GetValueOrDefault(FightKind.Protector)))));

        return [.. counts];
    }

    private ImmutableArray<GameText> WarningsOf(HarvestZone zone, CharacterState state)
    {
        var warnings = new List<GameText>();

        if (ChestWarning is { } chest)
            warnings.Add(GameText.Of(HarvestTexts.Text(chest)));

        if (_settings.Mode is HarvestMode.ByProfession)
            return [.. warnings];

        foreach (var item in _settings.Resources)
        {
            var lowest = zone.Targets.Values.Where(target => target.ItemId == item).OrderBy(target => target.Level).FirstOrDefault();

            if (lowest.SkillId is 0)
                continue;

            var level = MapHarvest.LevelOf(state, lowest.JobId);

            if (level < lowest.Level)
            {
                warnings.Add(GameText.Of(
                    HarvestTexts.Text("WarningLevel"),
                    GameTextValue.Item(item),
                    GameTextValue.Job(lowest.JobId),
                    GameTextValue.Number(lowest.Level),
                    GameTextValue.Number(level)));
            }
        }

        return [.. warnings];
    }
}
