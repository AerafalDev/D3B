namespace D3B.Studio.Behaviors;

/// <summary>An action's code, its values boxed, as the run calls it.</summary>
internal interface IActionNode
{
    bool IsInstant { get; }

    bool? IsReached(BehaviorView view, object values);

    Task RunAsync(ActionContext context, object values, CancellationToken cancellationToken);
}
