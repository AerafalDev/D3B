using D3B.Bot.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Behaviors.Nodes;

/// <summary>The character in one of some sub-areas, or of some areas, by the sub-area the server names.</summary>
internal sealed class InPlace : ConditionNode<PlaceValues>
{
    private readonly IServiceProvider _services;

    public InPlace(IServiceProvider services)
    {
        _services = services;
    }

    public override bool Holds(BehaviorView view, PlaceValues values)
    {
        if (view.State.SubAreaId is not { } subArea)
            return false;

        if (values.SubAreas.Contains(subArea))
            return true;

        // The areas hold their sub-areas by the pack, read the first time a condition asks (D98).
        return values.Areas.Count > 0
            && _services.GetService<GameReferences>() is { } game
            && values.Areas.Any(area => game.IsWithin(SettingKind.SubArea, subArea, area));
    }
}
