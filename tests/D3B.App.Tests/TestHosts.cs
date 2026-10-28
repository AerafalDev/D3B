using D3B.App.Hosting;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests;

/// <summary>Hosts holding a test's bots, as the app's window reads them.</summary>
internal static class TestHosts
{
    /// <summary>A host of the bots a test plays, with its activities, never started: the window only reads its services.</summary>
    public static IHost Of(TestPlay play, params ActivityDefinition[] activities)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(play.Bots);
        builder.Services.AddSingleton(play.World);
        builder.Services.AddSingleton(new ActivityCatalog(activities));
        builder.Services.AddSingleton(new ClientBuild(TestInstall.Version, null));

        return builder.Build();
    }

    /// <summary>The app's runner of a host that started.</summary>
    public static async Task<HostRunner> RunningAsync(IHost host)
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(host, null)));

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is HostStatus.Running, TimeSpan.FromSeconds(10));

        return runner;
    }
}
