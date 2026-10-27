using D3B.Bot;
using D3B.Core.Modules;
using D3B.Core.Plugins;
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

    public static void LogComposition(IHost host, IReadOnlyList<IModule> modules)
    {
        var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BotHost).FullName!);
        var plugins = host.Services.GetServices<PluginInfo>().ToList();
        var names = string.Join(", ", modules.Select(module => module.GetType().Name));
        var directory = PluginsDirectory(host.Services.GetRequiredService<IConfiguration>());

        HostLog.ModulesBuilt(log, modules.Count, names);
        HostLog.PluginsLoaded(log, plugins.Count, directory);

        foreach (var plugin in plugins)
            HostLog.PluginLoaded(log, plugin.Name, plugin.Version, plugin.Id, plugin.Author ?? "unknown");
    }
}
