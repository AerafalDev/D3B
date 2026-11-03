using D3B.Bot.Activities;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors.Nodes;
using D3B.Bot.Hosting;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D3B.Bot.Behaviors;

/// <summary>Behaviors and the project that holds them (D296 to D300): the assets in their folder, the kinds of nodes, and the profiles started on bots.</summary>
/// <remarks>
/// Registers the <see cref="AssetLibrary"/> of <c>Studio:Path</c> (<c>%LOCALAPPDATA%\D3B\studio</c> by default), read before the
/// host starts with the behaviors and profiles given with the app (<see cref="GivenAssets"/>); the <see cref="NodeCatalog"/>, D3B's
/// conditions, events and actions among them (<see cref="CoreNodes"/>), an activity's node for each activity; the variables the
/// characters keep and the bots share; and the <see cref="ProfileStarter"/>. Requires the bots module.
/// </remarks>
public sealed class BehaviorsModule : IModule
{
    /// <summary>Registers what the module brings, for a host that builds its services apart, such as a test's.</summary>
    /// <param name="services">The host's services, which hold the activities' catalogue.</param>
    /// <param name="settings">Where the project's folder is.</param>
    public static void Register(IServiceCollection services, StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddSingleton(settings);
        services.AddSingleton<AssetLibrary>();
        services.TryAddSingleton<CharacterVariables>();
        services.TryAddSingleton<SharedVariables>();
        services.AddSingleton(provider => new NodeCatalog(provider.GetServices<NodeDefinition>(), provider.GetRequiredService<ActivityCatalog>(), provider));
        services.AddSingleton<ProfileStarter>();
        services.AddSingleton<IHostPreparation, AssetPreparation>();
        CoreNodes.Register(services);
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();

        Register(bot.Services, new StudioSettings
        {
            Path = Path.GetFullPath(bot.Configuration[StudioSettings.PathKey] ?? StudioSettings.DefaultPath),
            Recycles = bool.TryParse(bot.Configuration[StudioSettings.RecycleKey], out var recycles) && recycles,
            Watches = bool.TryParse(bot.Configuration[StudioSettings.WatchKey], out var watches) && watches,
        });
    }
}
