namespace D3B.Studio.Behaviors;

/// <summary>An instant action's code, its values boxed, as a rule plays it on the bot's loop: at once, with no task to wait for.</summary>
internal interface IInstantNode
{
    void Apply(ActionContext context, object values);
}
