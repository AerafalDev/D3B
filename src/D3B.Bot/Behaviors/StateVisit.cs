using D3B.Bot.Activities;
using D3B.Bot.Runtime;

namespace D3B.Bot.Behaviors;

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
    private volatile BotActivity? _current;

    /// <summary>Gets the leaf.</summary>
    public CompiledState State { get; }

    /// <summary>Gets whether the leaf's actions were played to their end.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Gets the activity the last step played, which the behavior's activity speaks for, its report, fullness and fights.</summary>
    public BotActivity? Current => _current;

    public StateVisit(CompiledState state, IServiceProvider services)
    {
        State = state;
        _services = services;
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
        IsFinished = false;
    }

    /// <summary>Plays a step: goes through the actions from the first, passing those already true, and plays the first that is not.</summary>
    /// <param name="context">The step's behavior context.</param>
    /// <param name="evaluate">Reads the bot as it is now.</param>
    /// <param name="control">The run, for what actions ask of it.</param>
    /// <param name="halted">Tells whether an action played at once paused or stopped the run, which ends the step.</param>
    /// <param name="cancellationToken">The step's token.</param>
    /// <returns>A task that completes once an action is played, or the actions are at their end.</returns>
    public async Task StepAsync(BotContext context, Func<Evaluation> evaluate, IBehaviorControl control, Func<bool> halted, CancellationToken cancellationToken)
    {
        var step = State.Entry;

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

                        if (!await running.StepAsync(context, cancellationToken).ConfigureAwait(false))
                            _done.Add(activity.Id);

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
                        await action.Node.RunAsync(new ActionContext(context, evaluation.View, control), values, cancellationToken).ConfigureAwait(false);

                        if (reached is null)
                            _done.Add(action.Id);

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
