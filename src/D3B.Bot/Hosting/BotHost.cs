using D3B.Bot.Activities;
using D3B.Bot.Data;
using D3B.Bot.Pages;
using D3B.Bot.Protocol;
using D3B.Core.Modules;
using D3B.Core.Plugins;
using D3B.Data.Packs;
using D3B.Network.Relay;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>Assembles the bots' host, the default modules then the plugins, and starts it once its game data and protocol are ready.</summary>
/// <remarks>
/// The app runs this host in its own process, and <c>D3B.Host</c> runs it without the app, for measurements and tests (D91).
/// </remarks>
public static class BotHost
{
    /// <summary>The setting naming the directory plugins load from, relative to the host's executable.</summary>
    public const string PluginsDirectoryKey = "Plugins:Directory";

    /// <summary>Creates the builder of the bots' host: the default modules, then the plugins.</summary>
    /// <param name="args">The command-line arguments, read as configuration.</param>
    /// <returns>The builder, to which a caller may add modules before it starts the host.</returns>
    /// <exception cref="PluginLoadException">A plugin is invalid.</exception>
    public static BotBuilder CreateBuilder(string[] args)
    {
        var bot = new BotBuilder(args);

        bot.AddModules(new DefaultModules());
        bot.AddPlugins(PluginsDirectory(bot.Configuration));

        return bot;
    }

    /// <summary>Gets the directory plugins load from.</summary>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns><c>Plugins:Directory</c>, else <c>plugins</c>, from the executable's directory.</returns>
    public static string PluginsDirectory(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Plugins ship next to the host, so a relative path never depends on the directory the host is started from.
        return Path.GetFullPath(configuration[PluginsDirectoryKey] ?? "plugins", AppContext.BaseDirectory);
    }

    /// <summary>Builds the host, gets the game data and the protocol ready, then starts it.</summary>
    /// <param name="bot">The host's builder.</param>
    /// <param name="cancellationToken">Stops getting the host ready, such as a pack's download.</param>
    /// <returns>
    /// The host, started, which the caller stops and disposes; or why it did not start, for lack of game data, of a mapping
    /// that holds, or of the address its relay listens on, which is also logged.
    /// </returns>
    public static async Task<BotHostStart> StartAsync(BotBuilder bot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var host = bot.Build();

        try
        {
            LogComposition(host, bot.Modules);

            var failure = PrepareCatalogs(host)
                ?? await PrepareDataAsync(host, cancellationToken).ConfigureAwait(false)
                ?? await PrepareProtocolAsync(host, cancellationToken).ConfigureAwait(false)
                ?? await StartHostAsync(host, cancellationToken).ConfigureAwait(false);

            if (failure is null)
                return new BotHostStart(host, null);

            host.Dispose();

            return new BotHostStart(null, failure);
        }
        catch
        {
            host.Dispose();

            throw;
        }
    }

    /// <summary>Starts the host as <see cref="StartAsync"/> does, then runs it until it is stopped.</summary>
    /// <param name="bot">The host's builder.</param>
    /// <param name="cancellationToken">Stops the host.</param>
    /// <returns>
    /// The process's exit code: 0 once stopped, 1 when the host did not start for lack of game data, of a mapping that
    /// holds, or of the address its relay listens on.
    /// </returns>
    public static async Task<int> RunAsync(BotBuilder bot, CancellationToken cancellationToken)
    {
        var start = await StartAsync(bot, cancellationToken).ConfigureAwait(false);

        if (start.Host is not { } host)
            return 1;

        using (host)
            await host.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);

        return 0;
    }

    private static void LogComposition(IHost host, IReadOnlyList<IModule> modules)
    {
        var log = Logger(host);
        var plugins = host.Services.GetServices<PluginInfo>().ToList();
        var names = string.Join(", ", modules.Select(module => module.GetType().Name));
        var directory = PluginsDirectory(host.Services.GetRequiredService<IConfiguration>());

        HostLog.ModulesBuilt(log, modules.Count, names);
        HostLog.PluginsLoaded(log, plugins.Count, directory);

        foreach (var plugin in plugins)
            HostLog.PluginLoaded(log, plugin.Name, plugin.Version, plugin.Id, plugin.Author ?? "unknown");
    }

    private static string? PrepareCatalogs(IHost host)
    {
        try
        {
            // Before the host starts: two activities or pages sharing an id, however registered, stop it here, rather than
            // the app that reads the catalogues later, which holds every client's connection (D91).
            host.Services.GetService<ActivityCatalog>();
            host.Services.GetService<PageCatalog>();

            return null;
        }
        catch (InvalidOperationException exception)
        {
            var log = Logger(host);

            HostLog.DoesNotStart(log, exception.Message);

            return exception.Message;
        }
    }

    private static async Task<string?> PrepareDataAsync(IHost host, CancellationToken cancellationToken)
    {
        // A host whose data module was disabled runs without game data.
        if (host.Services.GetService<PackSource>() is not { } source)
            return null;

        var log = Logger(host);

        try
        {
            // Before the host starts, so that no service reads the pack before it is in place.
            await source.EnsureAsync(cancellationToken).ConfigureAwait(false);

            var pack = host.Services.GetRequiredService<GamePack>();

            HostLog.PackOpened(log, pack.Info.ClientVersion, GamePack.FormatVersion, source.PackPath);

            return null;
        }
        catch (DataUnavailableException exception)
        {
            HostLog.DoesNotStart(log, exception.Message);

            return exception.Message;
        }
    }

    private static async Task<string?> PrepareProtocolAsync(IHost host, CancellationToken cancellationToken)
    {
        // A host whose protocol module was disabled, and that no module requires, reads no client's wire.
        if (host.Services.GetService<ProtocolSource>() is not { } source)
            return null;

        var log = Logger(host);

        try
        {
            // The version gate (D11): before the host starts, so that no connection is relayed without a mapping that holds.
            await source.EnsureAsync(cancellationToken).ConfigureAwait(false);

            var translation = host.Services.GetRequiredService<ProtocolTranslation>();
            var audit = translation.Audit;

            HostLog.MappingLoaded(log, translation.ClientVersion, MappingFile.FormatVersion, audit.Messages, audit.Fields, audit.Enums, audit.Values, source.MappingPath);

            return null;
        }
        catch (ProtocolUnavailableException exception)
        {
            HostLog.DoesNotStart(log, exception.Message);

            return exception.Message;
        }
    }

    private static async Task<string?> StartHostAsync(IHost host, CancellationToken cancellationToken)
    {
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (RelayUnavailableException exception)
        {
            var log = Logger(host);

            HostLog.DoesNotStart(log, exception.Message);

            return exception.Message;
        }
    }

    private static ILogger Logger(IHost host)
    {
        return host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BotHost).FullName!);
    }
}
