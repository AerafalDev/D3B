using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Settings;
using D3B.Bot.Zaaps;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Zaaps;

public sealed class ZaapsModuleTests
{
    private static ActivityDefinition Discovery()
    {
        var services = new ServiceCollection();

        ZaapsModule.Register(services);

        return Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
    }

    [Fact]
    public void TheModuleRegistersTheDiscoveryWithNoSetting()
    {
        var discovery = Discovery();

        Assert.Equal(ZaapsModule.ActivityId, discovery.Id);
        Assert.Same(ActivitySettings.None, discovery.Settings);
    }

    [Fact]
    public void TheDiscoveryIsNamedInFrench()
    {
        Assert.Equal("Découvrir les zaaps", Discovery().Name.In(CultureInfo.GetCultureInfo("fr")));
    }

    [Fact]
    public void TheDiscoveryIsMadeFromNoService()
    {
        using var services = new ServiceCollection().BuildServiceProvider();

        Assert.IsType<ZaapDiscoveryActivity>(Discovery().Create(services));
    }
}
