using D3B.Bot.Behaviors;

namespace D3B.Bot.Routes;

/// <summary>An action of a route's map whose play is not made yet: the editor holds it, the run says so (D377).</summary>
/// <typeparam name="TValues">Its values.</typeparam>
internal sealed class UnplayedAction<TValues> : ActionNode<TValues>
    where TValues : class, new()
{
    public override Task RunAsync(ActionContext context, TValues values, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("This action of a route is not played yet.");
    }
}
