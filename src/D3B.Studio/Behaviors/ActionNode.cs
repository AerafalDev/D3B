namespace D3B.Studio.Behaviors;

/// <summary>An action a module declares: a goal the state says reached or not, or an action played whole (D297, D298).</summary>
/// <typeparam name="TValues">Its values, a class the module describes as an activity's settings (D98).</typeparam>
/// <remarks>
/// <para>
/// No position is kept (D87): a state's actions are gone through again from the first at each step, so that one whose
/// goal is reached, being on a map, the inventory deposited, is passed. One that says nothing of its goal, talking to an
/// NPC, is played once each time the state is entered, and again if an interruption cut it.
/// </para>
/// <para>One per host, made once: what it keeps of one play is its values' and the bot's.</para>
/// </remarks>
public abstract class ActionNode<TValues> : IActionNode
    where TValues : class, new()
{
    /// <summary>Gets whether the action plays at once, never waiting: such an action may play in a rule.</summary>
    public virtual bool IsInstant => false;

    /// <summary>Tells whether the action's goal is reached, so that it is passed.</summary>
    /// <param name="view">What the conditions read.</param>
    /// <param name="values">Its values.</param>
    /// <returns>Whether it is reached; <see langword="null"/>, by default, when the state says nothing of it.</returns>
    public virtual bool? IsReached(BehaviorView view, TValues values)
    {
        return null;
    }

    /// <summary>Plays the action: a step of the behavior.</summary>
    /// <param name="context">What it plays with.</param>
    /// <param name="values">Its values.</param>
    /// <param name="cancellationToken">The step's token: an interruption, an urgent state or the run's stop cancels it.</param>
    /// <returns>A task that completes once played.</returns>
    public abstract Task RunAsync(ActionContext context, TValues values, CancellationToken cancellationToken);

    bool? IActionNode.IsReached(BehaviorView view, object values)
    {
        return IsReached(view, (TValues)values);
    }

    Task IActionNode.RunAsync(ActionContext context, object values, CancellationToken cancellationToken)
    {
        return RunAsync(context, (TValues)values, cancellationToken);
    }
}
