using D3B.Bot;
using D3B.Bot.Data;
using D3B.Core.Modules;
using D3B.Core.Plugins;
using D3B.Data.Packs;
using D3B.Network.Relay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Host;

/// <summary>Assembles the headless host: the default modules, then the plugins.</summary>
internal static class BotHost
{
    public const string PluginsDirectoryKey = "Plugins:Directory";

    public static BotBuilder CreateBuilder(string[] args)
    {
        var bot = new BotBuilder(args);

        bot.AddModules(new DefaultModules());
        bot.AddPlugins(PluginsDirectory(bot.Configuration));

        return bot;
    }

    public static string PluginsDirectory(IConfiguration configuration)
    {
        // Plugins ship next to the host, so a relative path never depends on the directory the host is started from.
        return Path.GetFullPath(configuration[PluginsDirectoryKey] ?? "plugins", AppContext.BaseDirectory);
    }

    /// <summary>Builds the host, gets the game data ready, then runs until the host is stopped.</summary>
    /// <returns>
    /// The process's exit code: 0 once stopped, 1 when the host did not start for lack of game data or of the address its
    /// relay listens on.
    /// </returns>
    public static async Task<int> RunAsync(BotBuilder bot, CancellationToken cancellationToken)
    {
        using var host = bot.Build();

        LogComposition(host, bot.Modules);

        if (!await PrepareDataAsync(host, cancellationToken))
            return 1;

        try
        {
            await host.StartAsync(cancellationToken);
        }
        catch (RelayUnavailableException exception)
        {
            var log = Logger(host);

            HostLog.DoesNotStart(log, exception.Message);

            return 1;
        }

        await host.WaitForShutdownAsync(cancellationToken);

        return 0;
    }

    public static void LogComposition(IHost host, IReadOnlyList<IModule> modules)
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

    private static async Task<bool> PrepareDataAsync(IHost host, CancellationToken cancellationToken)
    {
        // A host whose data module was disabled runs without game data.
        if (host.Services.GetService<PackSource>() is not { } source)
            return true;

        var log = Logger(host);

        try
        {
            // Before the host starts, so that no service reads the pack before it is in place.
            await source.EnsureAsync(cancellationToken);

            var pack = host.Services.GetRequiredService<GamePack>();

            HostLog.PackOpened(log, pack.Info.ClientVersion, GamePack.FormatVersion, source.PackPath);

            return true;
        }
        catch (DataUnavailableException exception)
        {
            HostLog.DoesNotStart(log, exception.Message);

            return false;
        }
    }

    private static ILogger Logger(IHost host)
    {
        return host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BotHost).FullName!);
    }
}
