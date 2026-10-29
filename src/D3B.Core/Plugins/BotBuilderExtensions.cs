using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Core.Plugins;

/// <summary>Adds external plugins to a <see cref="BotBuilder"/>.</summary>
public static class BotBuilderExtensions
{
    /// <summary>Loads the plugins under a directory, registers their <see cref="PluginInfo"/> and adds their root modules.</summary>
    /// <remarks>
    /// <para>
    /// Each folder under <paramref name="directory"/> is a plugin, holding an assembly named after the folder and its
    /// dependencies. Each loads once into its own context, which shares the host's assemblies (D6). Plugins are added in
    /// the order of their folder names. A missing directory holds no plugin.
    /// </para>
    /// <para>
    /// A folder that does not hold a valid plugin stops the whole load, rather than leaving the bots without a feature
    /// they were given.
    /// </para>
    /// </remarks>
    /// <param name="bot">The application being built.</param>
    /// <param name="directory">The plugins directory.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="PluginLoadException">
    /// A folder does not hold a valid plugin, two plugins share an id, or a plugin's modules need an assembly neither the
    /// host nor the plugin holds.
    /// </exception>
    public static BotBuilder AddPlugins(this BotBuilder bot, string directory)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        foreach (var plugin in PluginLoader.Load(directory))
        {
            bot.Services.AddSingleton(plugin.Info);

            try
            {
                bot.AddModule(plugin.Module);
            }
            catch (Exception exception) when (IsMissingAssembly(exception))
            {
                // The loader reads the root module alone (D97): what the rest of the plugin needs loads as its modules build.
                throw new PluginLoadException($"The plugin '{plugin.Info.Name}' references an assembly that neither the host nor the plugin holds.", exception);
            }
        }

        return bot;
    }

    private static bool IsMissingAssembly(Exception exception)
    {
        // An assembly the runtime could not load is named by its display name, version included; a file is named by its path.
        return exception switch
        {
            TypeLoadException => true,
            FileNotFoundException { FileName: { } name } => name.Contains(", Version=", StringComparison.Ordinal),
            FileLoadException { FileName: { } name } => name.Contains(", Version=", StringComparison.Ordinal),
            _ => false,
        };
    }
}
