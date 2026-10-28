using System.Globalization;
using System.Net;
using System.Net.Sockets;
using D3B.Bot.Tests;
using D3B.Core.Modules;
using D3B.Data.Releases;
using D3B.Data.Tests.Releases;
using D3B.Network.Relay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Host.Tests;

public sealed class BotHostTests : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly TestInstall _install = new();
    private readonly FakeLogCollector _log = new();

    private static bool IsStarted(FakeLogRecord record)
    {
        return record.Category == "Microsoft.Hosting.Lifetime" && record.Message.StartsWith("Application started", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public async Task TheHostStartsOnThePackOfTheInstalledBuildAndStopsCleanly()
    {
        _install.WritePack();
        _install.WriteProtocol();

        var bot = CreateBuilder();

        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(_log.GetSnapshot(), record => record.Message == $"Opened the pack of client {TestInstall.Version}, format 3: {_install.PackPath}");
        Assert.Contains(_log.GetSnapshot(), record => record.Message == $"Loaded the mapping of client {TestInstall.Version}, format 1: 37 messages, 92 fields, 9 enums and 60 values audited, {_install.MappingPath}");
        Assert.Contains(_log.GetSnapshot(), IsStarted);
        Assert.Contains(_log.GetSnapshot(), record => record.Message.StartsWith("Clients reach D3B: their configuration sends localhost to 127.0.13.59", StringComparison.Ordinal));
        Assert.EndsWith($"ConfigUrl={_install.ConfigUrl}\n", File.ReadAllText(_install.VersionFile), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostDoesNotStartWithoutAPack()
    {
        var bot = CreateBuilder();

        // Nothing is published: the download fails as it would for a client build without a release.
        bot.Services.AddSingleton(_ => new ReleaseDownloader(ClientRelease.DefaultRepository, new FakeGitHub()));
        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
        Assert.Contains($"No pack for client {TestInstall.Version}", failure.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/extract.cs", failure.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(_install.PackPath));
    }

    [Fact]
    public async Task TheHostDoesNotStartWithoutAMapping()
    {
        _install.WritePack();

        var bot = CreateBuilder();

        // Nothing is published: a new build, which no one has mapped yet.
        bot.Services.AddSingleton(_ => new ReleaseDownloader(ClientRelease.DefaultRepository, new FakeGitHub()));
        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
        Assert.Contains($"No schema for client {TestInstall.Version}", failure.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/schema.cs", failure.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/mapping.cs", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostDoesNotStartOnAMappingThatDoesNotHold()
    {
        _install.WritePack();
        _install.WriteProtocol(edit: mapping => mapping.Messages.RemoveAt(0));

        var bot = CreateBuilder();

        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
        Assert.StartsWith($"The host does not start. The mapping of client {TestInstall.Version} does not hold: ", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostDoesNotStartWhenItsRelayCannotListen()
    {
        _install.WritePack();
        _install.WriteProtocol();

        // Another program holding the port on D3B's address, as one might hold 443.
        using var holder = new TcpListener(RelaySettings.DefaultAddress, 0);

        holder.Start();

        var port = ((IPEndPoint)holder.LocalEndpoint).Port;
        var bot = CreateBuilder("--Network:Ports:0", port.ToString(CultureInfo.InvariantCulture));

        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
        Assert.Contains($"D3B cannot listen on {RelaySettings.DefaultAddress} on ports {port}", failure.Message, StringComparison.Ordinal);
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

    private BotBuilder CreateBuilder(params string[] arguments)
    {
        var bot = BotHost.CreateBuilder([.. _install.Arguments, "--Plugins:Directory", Path.Combine(AppContext.BaseDirectory, "no-plugins"), .. arguments]);

        bot.Logging.AddProvider(new FakeLoggerProvider(_log));

        return bot;
    }
}
