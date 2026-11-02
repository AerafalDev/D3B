namespace D3B.Bot.Behaviors;

/// <summary>A condition's code, its values boxed, as the run calls it.</summary>
internal interface IConditionNode
{
    bool Holds(BehaviorView view, object values);
}
