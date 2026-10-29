using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using D3B.Scripting.Library;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Scripting;

/// <summary>The scripts users extend the bot with: those of a folder, listed as activities while the host runs (D102, D108).</summary>
/// <remarks>
/// The app and <c>D3B.Host</c> both add it to the host's composition, as the storage module, so both list the scripts of
/// <c>Scripting:Directory</c>, <c>%LOCALAPPDATA%\D3B\scripts</c> by default. Its <see cref="ScriptLibrary"/> is a source
/// of the <see cref="ActivityCatalog"/>: a profile names a script as it names a module's activity (D99).
/// </remarks>
public sealed class ScriptingModule : IModule
{
    internal static void Register(IServiceCollection services, ScriptingSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<ScriptLibrary>();
        services.AddSingleton<IActivitySource>(provider => provider.GetRequiredService<ScriptLibrary>());
        services.AddHostedService(provider => provider.GetRequiredService<ScriptLibrary>());
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();

        var directory = bot.Configuration[ScriptingSettings.DirectoryKey] ?? ScriptingSettings.DefaultDirectory;

        Register(bot.Services, new ScriptingSettings { Directory = Path.GetFullPath(directory) });
    }
}
