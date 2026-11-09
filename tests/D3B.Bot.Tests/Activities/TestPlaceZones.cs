using D3B.Bot.Activities;

namespace D3B.Bot.Tests.Activities;

internal sealed class TestPlaceZones : IPlaceZones
{
    private readonly Dictionary<string, IReadOnlySet<long>> _zones = [];

    public TestPlaceZones Add(string zone, params long[] maps)
    {
        _zones[zone] = maps.ToHashSet();

        return this;
    }

    public bool TryResolve(string zone, out IReadOnlySet<long> maps)
    {
        return _zones.TryGetValue(zone, out maps!);
    }
}
