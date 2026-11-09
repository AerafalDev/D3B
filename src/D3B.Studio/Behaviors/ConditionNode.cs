namespace D3B.Studio.Behaviors;

/// <summary>A condition a module declares: a pure read of the state, true or false (D298).</summary>
/// <typeparam name="TValues">Its values, a class the module describes as an activity's settings (D98).</typeparam>
/// <remarks>
/// One per host, made once, called from the bot's loop as well as its steps: it reads, keeps nothing, never waits and
/// never throws for a state it cannot say of, which does not hold.
/// </remarks>
public abstract class ConditionNode<TValues> : IConditionNode
    where TValues : class, new()
{
    /// <summary>Tells whether the condition holds.</summary>
    /// <param name="view">What it reads.</param>
    /// <param name="values">Its values.</param>
    /// <returns>Whether it holds.</returns>
    public abstract bool Holds(BehaviorView view, TValues values);

    bool IConditionNode.Holds(BehaviorView view, object values)
    {
        return Holds(view, (TValues)values);
    }
}
