using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Runtime;

namespace D3B.Studio.Behaviors;

/// <summary>A leaf of a behavior while it stays chosen: its actions gone through from the first at each step (D87, D297).</summary>
/// <remarks>
/// <para>
/// No position is kept: an action whose goal is reached is passed, so that a leaf entered again, after a fight, a new
/// connection or another state, passes what is already true. An action the state says nothing of is played once in the
/// visit, and again if an interruption cut it: what the visit remembers is what was played to its end, never where it was.
/// </para>
/// <para>
/// An activity's node keeps its activity for the visit, so that harvesting goes on with its tour as it did when it was the
/// bot's activity; it is done once a step says so. The visit is over once another leaf is chosen.
/// </para>
/// </remarks>
internal sealed class StateVisit : IDisposable
{
    private readonly HashSet<string> _done = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BotActivity> _activities = new(StringComparer.Ordinal);
    private readonly IServiceProvider _services;
    private readonly ImmutableArray<string> _list;
    private volatile BotActivity? _current;
    private volatile string? _at;

    /// <summary>Gets the leaf.</summary>
    public CompiledState State { get; }

    /// <summary>Gets whether the leaf's actions were played to their end.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Gets the activity the last step played, which the behavior's activity speaks for, its report, fullness and fights.</summary>
    public BotActivity? Current => _current;

    /// <summary>Gets the step being played, until it returns, for the trace to say which one a cut or a failure ended.</summary>
    public PlayedStep? InFlight { get; private set; }

    /// <summary>Gets the step the leaf is at and how many it has, when it is a plain list of steps that follow one another with no branch: the step last begun, counted from 0 (D419).</summary>
    public (int Step, int Total)? Place => _list.Length > 1 ? (_at is { } at ? Math.Max(0, _list.IndexOf(at)) : 0, _list.Length) : null;

    public StateVisit(CompiledState state, IServiceProvider services)
    {
        State = state;
        _services = services;
        _list = ListOf(state);
    }

    private static ImmutableArray<string> ListOf(CompiledState state)
    {
        var ids = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var step = state.Entry; step is not null; step = step.Next)
        {
            if (step is not (CompiledStep.Action or CompiledStep.Activity) || step.Otherwise is not null || !seen.Add(step.Id))
                return [];

            ids.Add(step.Id);
        }

        return ids.ToImmutable();
    }

    /// <summary>Starts the leaf's actions again, once played to their end, for a state that repeats.</summary>
    public void Restart()
    {
        foreach (var id in _done.Where(_activities.ContainsKey).ToList())
        {
            (_activities[id] as IDisposable)?.Dispose();
            _activities.Remove(id);
        }

        _done.Clear();
        _at = null;
        IsFinished = false;
    }

    /// <summary>Plays a step: goes through the actions from the first, passing those already true, and plays the first that is not.</summary>
    /// <param name="context">The step's behavior context.</param>
    /// <param name="evaluate">Reads the bot as it is now.</param>
    /// <param name="control">The run, for what actions ask of it.</param>
    /// <param name="halted">Tells whether an action played at once paused or stopped the run, which ends the step.</param>
    /// <param name="played">Told of each action or activity's step played, for the trace.</param>
    /// <param name="cancellationToken">The step's token.</param>
    /// <returns>A task that completes once an action is played, or the actions are at their end.</returns>
    public async Task StepAsync(BotContext context, Func<Evaluation> evaluate, IBehaviorControl control, Func<bool> halted, Action<PlayedStep> played, CancellationToken cancellationToken)
    {
        var step = State.Entry;

        InFlight = null;

        while (step is not null)
        {
            var evaluation = evaluate();

            switch (step)
            {
                case CompiledStep.Branch branch:
                    step = branch.Condition(evaluation) ? branch.Next : branch.Otherwise;

                    continue;

                case CompiledStep.SetVariable set:
                    if (_done.Add(set.Id))
                        evaluation.Variables.Set(set.Variable, set.Value(evaluation));

                    step = set.Next;

                    continue;

                case CompiledStep.Activity activity when _done.Contains(activity.Id):
                    step = activity.Next;

                    continue;

                case CompiledStep.Activity activity:
                    {
                        if (!_activities.TryGetValue(activity.Id, out var running))
                        {
                            running = activity.Definition.Create(_services, activity.Settings.Resolve(evaluation));
                            _activities[activity.Id] = running;
                        }

                        _current = running;
                        _at = activity.Id;
                        InFlight = new PlayedStep(activity.Definition.Name, null, false);

                        var goesOn = await running.StepAsync(context, cancellationToken).ConfigureAwait(false);

                        if (!goesOn)
                            _done.Add(activity.Id);

                        InFlight = null;
                        played(new PlayedStep(activity.Definition.Name, running.Report?.Doing, !goesOn));

                        return;
                    }

                case CompiledStep.Action action:
                    {
                        var values = action.Values.Resolve(evaluation)!;
                        var reached = action.Node.IsReached(evaluation.View, values);

                        if (reached is true || (reached is null && _done.Contains(action.Id)))
                        {
                            step = action.Next;

                            continue;
                        }

                        _current = null;
                        _at = action.Id;
                        InFlight = new PlayedStep(action.Sentence, null, false);
                        await action.Node.RunAsync(new ActionContext(context, evaluation.View, control), values, cancellationToken).ConfigureAwait(false);
                        InFlight = null;

                        if (reached is null)
                            _done.Add(action.Id);

                        played(new PlayedStep(action.Sentence, null, true));

                        // An action played at once is no step of its own: the way goes on, unless it paused or stopped the run.
                        if (!action.IsInstant || halted())
                            return;

                        step = action.Next;

                        continue;
                    }

                default:
                    step = step.Next;

                    continue;
            }
        }

        IsFinished = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var activity in _activities.Values)
            (activity as IDisposable)?.Dispose();

        _activities.Clear();
        _current = null;
    }
}
