using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.Host.Tests;

public sealed class BotHostTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task TheHostStartsAndStopsCleanly()
    {
        var bot = BotHost.CreateBuilder(["--Plugins:Directory", Path.Combine(AppContext.BaseDirectory, "no-plugins")]);

        using var host = bot.Build();

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var started = new TaskCompletionSource();

        using var registration = lifetime.ApplicationStarted.Register(started.SetResult);

        var run = host.RunAsync(TestContext.Current.CancellationToken);

        await started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        lifetime.StopApplication();
        await run.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
    }

    [Fact]
    public void PluginsLiveNextToTheHostByDefault()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "plugins"), BotHost.PluginsDirectory(configuration));
    }

    [Fact]
    public void ARelativePluginsDirectoryStartsFromTheHost()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>(BotHost.PluginsDirectoryKey, "extra")])
            .Build();

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "extra"), BotHost.PluginsDirectory(configuration));
    }
}
