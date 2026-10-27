using D3B.Bot.Data;
using D3B.Bot.Network;
using D3B.Core.Modules;
using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Tests;

public sealed class DefaultModulesTests
{
    [Fact]
    public void TheDataModuleBuildsFirst()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules());

        Assert.IsType<DataModule>(bot.Modules[0]);
    }

    [Fact]
    public void TheNetworkModuleBuildsAfterTheDataModule()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules());

        Assert.IsType<NetworkModule>(bot.Modules[1]);
    }

    [Fact]
    public void TheDataModuleCanBeDisabled()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules(), group => group.Disable<DataModule>());

        Assert.False(bot.HasModule<DataModule>());
    }
}
