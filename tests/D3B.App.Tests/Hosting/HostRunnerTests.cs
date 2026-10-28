using D3B.App.Hosting;
using D3B.Bot.Hosting;
using D3B.Bot.Runtime;
using D3B.Bot.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.Hosting;

public sealed class HostRunnerTests : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly TestInstall _install = new();

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public async Task TheHostRunsInTheAppsProcessUntilItStopsThenGivesTheClientItsConfigurationBack()
    {
        _install.WritePack();
        _install.WriteProtocol();

        var runner = new HostRunner([.. _install.Arguments, "--Plugins:Directory", Path.Combine(AppContext.BaseDirectory, "no-plugins")]);

        Assert.Equal(HostStatus.Starting, runner.Status);

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, _timeout);

        Assert.Equal(HostStatus.Running, runner.Status);
        Assert.NotNull(runner.Services?.GetService<Bots>());
        Assert.DoesNotContain($"ConfigUrl={_install.ConfigUrl}\n", File.ReadAllText(_install.VersionFile), StringComparison.Ordinal);

        await runner.StopAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(HostStatus.Stopped, runner.Status);
        Assert.Null(runner.Services);
        Assert.EndsWith($"ConfigUrl={_install.ConfigUrl}\n", File.ReadAllText(_install.VersionFile), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHostThatDidNotStartSaysWhy()
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(null, "No pack for client 3.7.12.12.")));

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, _timeout);

        Assert.Equal(HostStatus.Failed, runner.Status);
        Assert.Equal("No pack for client 3.7.12.12.", runner.Failure);
        Assert.Null(runner.Services);

        await runner.StopAsync();

        Assert.Equal(HostStatus.Failed, runner.Status);
    }

    [Fact]
    public async Task AHostThatCouldNotBeBuiltSaysWhy()
    {
        var runner = new HostRunner(_ => throw new InvalidOperationException("Two plugins have the id sample."));

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, _timeout);

        Assert.Equal(HostStatus.Failed, runner.Status);
        Assert.Equal("Two plugins have the id sample.", runner.Failure);
    }

    [Fact]
    public async Task StoppingTheAppWhileTheHostGetsReadyCancelsIt()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new HostRunner(async cancellationToken =>
        {
            // As a pack's download would, until the app stops.
            using var registration = cancellationToken.Register(cancelled.SetResult);

            await Task.Delay(Timeout.Infinite, cancellationToken);

            return new BotHostStart(null, null);
        });

        runner.Start();
        await runner.StopAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.True(cancelled.Task.IsCompleted);
        Assert.Equal(HostStatus.Stopped, runner.Status);
    }

    [Fact]
    public void TheHostStopsOnceHoweverOftenTheAppStopsIt()
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(null, "No pack.")));

        Assert.Same(runner.StopAsync(), runner.StopAsync());
    }
}
