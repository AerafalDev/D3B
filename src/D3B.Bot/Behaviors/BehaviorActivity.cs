using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Journal;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;

namespace D3B.Bot.Behaviors;

/// <summary>The one activity that runs any behavior: each step, the leaf chosen from the state, then a step of it played (D297).</summary>
/// <remarks>
/// <para>
/// The choice is made again at each step's end. A leaf played to its end and chosen again ends the run, done, unless it
/// repeats; when no state holds, the run waits, a few seconds at a time, for the state, a variable or the time to choose
/// one, as a planning's hours will (D298). On the bot's loop (<see cref="Watch"/>), the
/// events start their rules, and a state marked urgent that comes to hold cuts the step running, as an interruption does (D9).
/// </para>
/// <para>
/// An activity's node delegates to today's activity (D297): while one plays, the behavior speaks for it, its report,
/// statistics, fullness and unloading, interruptions and fights. Out of one, a fight is the AI's, which plays every turn
/// (D301), and an interruption resumes.
/// </para>
/// </remarks>
internal sealed class BehaviorActivity : BotActivity, IDisposable, IBehaviorControl
{
    private static readonly TimeSpan _idleAtMost = TimeSpan.FromSeconds(5);

    private readonly CompiledBehavior _behavior;
    private readonly string _name;
    private readonly IServiceProvider _services;
    private readonly CharacterVariables _characters;
    private readonly SharedVariables _shared;
    private readonly Lock _attaching = new();
    private readonly ImmutableArray<OutlineState> _always;
    private readonly ImmutableArray<OutlineState> _states;
    private volatile BotActor? _bot;
    private volatile BehaviorVariables? _variables;
    private volatile StateVisit? _visit;
    private volatile CancellationTokenSource? _cut;
    private volatile bool _idle;
    private volatile bool _stopping;
    private volatile bool _halt;
    private bool _entered;
    private DateTimeOffset _started;

    // Only the bot's loop reads and writes it.
    private BehaviorView? _seen;

    /// <inheritdoc/>
    public override string Name => _name;

    /// <inheritdoc/>
    public override ActivityReport? Report => _visit is { } visit
        ? visit.Current?.Report ?? new ActivityReport { Doing = GameText.Of(BehaviorTexts.Text("Doing"), GameTextValue.Of(visit.State.Name)) }
        : null;

    /// <inheritdoc/>
    public override ActivityStatistics? Statistics => _visit?.Current?.Statistics;

    /// <inheritdoc/>
    public override ImmutableArray<ActivityWatch> Watches => _visit?.Current?.Watches ?? [];

    /// <inheritdoc/>
    public override BehaviorOutline? Outline => new(_always, _states, _visit?.State.Id);

    /// <inheritdoc/>
    public override int TeleportChanges => _visit?.Current?.TeleportChanges ?? 1;

    /// <inheritdoc/>
    public override bool FightsItsFights => _visit?.Current?.FightsItsFights ?? true;

    /// <summary>Gets the activity an activity's node plays now, which the behavior speaks for, if one does.</summary>
    internal BotActivity? Playing => _visit?.Current;

    public BehaviorActivity(CompiledBehavior behavior, string name, IServiceProvider services, CharacterVariables characters, SharedVariables shared)
    {
        _behavior = behavior;
        _name = name;
        _services = services;
        _characters = characters;
        _shared = shared;
        _always = Outlined(behavior.Always);
        _states = Outlined(behavior.States);
    }

    private static ImmutableArray<OutlineState> Outlined(ImmutableArray<CompiledState> states)
    {
        var outlined = ImmutableArray.CreateBuilder<OutlineState>();

        void Add(ImmutableArray<CompiledState> level, int depth)
        {
            foreach (var state in level)
            {
                outlined.Add(new OutlineState(state.Id, state.Name.Length > 0 ? state.Name : state.Id, depth, state.IsUrgent));
                Add(state.States, depth + 1);
            }
        }

        Add(states, 0);

        return outlined.ToImmutable();
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bot = context.Actor;

        Attach(bot);

        if (_stopping)
            return false;

        _halt = false;

        var leaf = _behavior.Choose(Evaluate(bot, bot.State));

        if (!_entered || leaf != _visit?.State)
            Enter(leaf, bot);

        // Played to its end and chosen again: the behavior is done, as StateTree's is, rather than wait on a state nothing changes.
        if (_visit is { IsFinished: true, State.Repeats: false })
            return false;

        using var cut = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _cut = cut;

        try
        {
            if (_visit is not { } visit)
            {
                _idle = true;
                await context.WaitUntilAsync(state => _behavior.Choose(Evaluate(bot, state)) != leaf, _idleAtMost, cut.Token).ConfigureAwait(false);
            }
            else
            {
                if (visit.IsFinished)
                    visit.Restart();

                await visit.StepAsync(context, () => Evaluate(bot, bot.State), this, () => _halt || _stopping, cut.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cut.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // An urgent state, a variable set, a pause or the run's stop cut the step: what it left open is settled first.
            await Recovery.SettleAsync(context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _cut = null;
            _idle = false;
        }

        return !_stopping;
    }

    /// <inheritdoc/>
    public override bool IsFull(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _visit?.Current?.IsFull(state) ?? false;
    }

    /// <inheritdoc/>
    public override Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        return _visit?.Current?.UnloadAsync(context, cancellationToken) ?? base.UnloadAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        return _visit?.Current?.OnInterrupted(reason) ?? InterruptionResponse.Resume;
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnFightOver(FightEnd? ending, InterruptionResponse response)
    {
        return _visit?.Current?.OnFightOver(ending, response) ?? response;
    }

    /// <inheritdoc/>
    public override GameText? Describe(Interruption reason)
    {
        return _visit?.Current?.Describe(reason);
    }

    /// <inheritdoc/>
    public override Task RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        return _visit?.Current?.RecoverAsync(context, reason, cancellationToken) ?? base.RecoverAsync(context, reason, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Watch(BotActor bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        Attach(bot);

        var view = View(bot, bot.State);
        var before = _seen;

        _seen = view;

        if (before is null || (ReferenceEquals(before.State, view.State) && before.IsConnected == view.IsConnected && before.FightsEnded == view.FightsEnded))
            return;

        var evaluation = new Evaluation(view, _variables!);

        if (!_behavior.Rules.IsEmpty)
        {
            var change = new BotChange(before, view);

            foreach (var rule in _behavior.Rules)
                Play(rule, change, evaluation, bot);
        }

        if (_behavior.HasUrgent && !_idle && _cut is { } cut && _behavior.Choose(evaluation) is { IsUrgent: true } urgent && urgent != _visit?.State)
        {
            BehaviorLog.Urgent(bot.Log, bot.Key.CharacterId, _name, urgent.Name);
            _ = cut.CancelAsync();
        }
    }

    /// <inheritdoc/>
    public void Pause()
    {
        _halt = true;
        _bot?.PauseAfterStep();
        _ = _cut?.CancelAsync();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _stopping = true;
        _ = _cut?.CancelAsync();
    }

    /// <inheritdoc/>
    public void Note(string text)
    {
        if (_bot is { } bot)
            bot.Journal.Add(new NoteEntry(bot.Time.GetUtcNow(), _name, text));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_variables is { } variables)
            variables.Changed -= OnVariableChanged;

        _visit?.Dispose();
        _visit = null;
    }

    private void Attach(BotActor bot)
    {
        if (_bot is not null)
            return;

        lock (_attaching)
        {
            if (_bot is not null)
                return;

            _started = bot.Time.GetUtcNow();
            _variables = new BehaviorVariables(_behavior.Variables, bot.Key, _characters, _shared);
            _variables.Changed += OnVariableChanged;
            _bot = bot;
        }
    }

    private BehaviorView View(BotActor bot, CharacterState state)
    {
        return new BehaviorView(state, bot.IsConnected, bot.LastEndedFight, bot.LastFightEnd, bot.Time.GetUtcNow(), _started);
    }

    private Evaluation Evaluate(BotActor bot, CharacterState state)
    {
        return new Evaluation(View(bot, state), _variables!);
    }

    private void Enter(CompiledState? leaf, BotActor bot)
    {
        _entered = true;
        _visit?.Dispose();
        _visit = leaf is null ? null : new StateVisit(leaf, _services);

        var name = leaf is null ? null : leaf.Name.Length > 0 ? leaf.Name : leaf.Id;

        if (name is null)
            BehaviorLog.NoState(bot.Log, bot.Key.CharacterId, _name);
        else
            BehaviorLog.StateEntered(bot.Log, bot.Key.CharacterId, _name, name);

        bot.Journal.Add(new StateEnteredEntry(bot.Time.GetUtcNow(), _name, name));
    }

    private void Play(CompiledRule rule, BotChange change, Evaluation evaluation, BotActor bot)
    {
        try
        {
            if (!rule.Event.Happened(change, rule.EventValues.Resolve(evaluation)!) || rule.When?.Holds(evaluation) == false)
                return;

            var context = new ActionContext(null, evaluation.View, this);

            // Every action of a rule is played at once, which the compiler checked: the way is gone through whole, on the loop.
            for (var step = rule.Entry; step is not null;)
            {
                switch (step)
                {
                    case CompiledStep.Branch branch:
                        step = branch.Condition(evaluation) ? branch.Next : branch.Otherwise;
                        break;
                    case CompiledStep.SetVariable set:
                        evaluation.Variables.Set(set.Variable, set.Value(evaluation));
                        step = set.Next;
                        break;
                    case CompiledStep.Action action:
                        action.Node.RunAsync(context, action.Values.Resolve(evaluation)!, CancellationToken.None).GetAwaiter().GetResult();
                        step = action.Next;
                        break;
                    default:
                        step = null;
                        break;
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            BehaviorLog.RuleFailed(bot.Log, bot.Key.CharacterId, _name, rule.Id, exception);
        }
    }

    private void OnVariableChanged(object? sender, EventArgs e)
    {
        // A leaf waiting for another to be chosen chooses again at once.
        if (_idle)
            _ = _cut?.CancelAsync();
    }
}
