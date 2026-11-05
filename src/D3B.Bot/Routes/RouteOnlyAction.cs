using D3B.Bot.Behaviors;

namespace D3B.Bot.Routes;

/// <summary>An action that only a route plays, because it needs where the route goes next: the editor holds it, a route's run plays it (D384).</summary>
/// <typeparam name="TValues">Its values.</typeparam>
internal sealed class RouteOnlyAction<TValues> : ActionNode<TValues>
    where TValues : class, new()
{
    public override Task RunAsync(ActionContext context, TValues values, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("This action is played by a route, which knows where it goes next.");
    }
}
