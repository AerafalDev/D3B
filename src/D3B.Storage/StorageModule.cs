using D3B.Bot.Behaviors;
using D3B.Bot.Fights;
using D3B.Bot.Harvesting;
using D3B.Bot.Hosting;
using D3B.Bot.Interactives;
using D3B.Bot.Npcs;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using D3B.Storage.Accounts;
using D3B.Storage.Fights;
using D3B.Storage.Graphics;
using D3B.Storage.Harvests;
using D3B.Storage.Npcs;
using D3B.Storage.Profiles;
using D3B.Storage.Runs;
using D3B.Storage.Variables;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace D3B.Storage;

/// <summary>D3B's database, SQLite through EF Core: the accounts and characters the relay saw, what the bots learned, the runs and the characters' variables (D99, D100, D138, D298).</summary>
/// <remarks>
/// <para>
/// The app and <c>D3B.Host</c> both add it to the host's composition, so both read and write one database, in
/// <c>Storage:Path</c> (<c>%LOCALAPPDATA%\D3B\d3b.db</c> by default). Its migrations apply before the host starts
/// (<see cref="IHostPreparation"/>): a database that does not open stops the host, which says why.
/// </para>
/// <para>
/// It registers the <see cref="AccountBook"/>, fed by the game connections it observes, each character naming the profile
/// asset it plays (D304), and starts a character's profile on its connection, when asked, through the behaviors module's
/// <see cref="ProfileStarter"/>. Activities do not see it. The <see cref="VariableRecorder"/> keeps the variables the
/// characters keep, as <see cref="CharacterVariables"/> (D298).
/// The <see cref="GraphicRecorder"/> keeps what the bots learn of the graphics, which they share as <see cref="GraphicSkills"/>,
/// the <see cref="RegrowthRecorder"/> how long the resources take to grow back, as <see cref="Regrowths"/> (D145), the
/// <see cref="PlacementRecorder"/> every fight's map and placement cells, as <see cref="FightPlacements"/> (D275), the
/// <see cref="NpcRecorder"/> the NPCs of the maps and what the bots learned of their dialogs, as <see cref="KnownNpcs"/> (D383), and the
/// <see cref="RunRecorder"/> each run of the bots' activities, which the <see cref="RunBook"/> gives the statistics (D146).
/// </para>
/// </remarks>
public sealed class StorageModule : IModule
{
    internal static void Register(IServiceCollection services, StorageSettings settings)
    {
        services.AddSingleton(settings);
        services.AddDbContextFactory<StorageContext>(options => options.UseSqlite(settings.ConnectionString));
        services.AddSingleton<AccountBook>();
        services.TryAddSingleton<CharacterVariables>();
        services.AddSingleton<VariableRecorder>();
        services.AddHostedService(provider => provider.GetRequiredService<VariableRecorder>());
        services.TryAddSingleton<GraphicSkills>();
        services.AddSingleton<GraphicRecorder>();
        services.AddHostedService(provider => provider.GetRequiredService<GraphicRecorder>());
        services.TryAddSingleton<Regrowths>();
        services.AddSingleton<RegrowthRecorder>();
        services.AddHostedService(provider => provider.GetRequiredService<RegrowthRecorder>());
        services.TryAddSingleton<FightPlacements>();
        services.AddSingleton<PlacementRecorder>();
        services.AddHostedService(provider => provider.GetRequiredService<PlacementRecorder>());
        services.TryAddSingleton<KnownNpcs>();
        services.AddSingleton<NpcRecorder>();
        services.AddHostedService(provider => provider.GetRequiredService<NpcRecorder>());
        services.AddSingleton<RunBook>();
        services.AddSingleton(provider => new RunRecorder(provider.GetRequiredService<IDbContextFactory<StorageContext>>(), provider.GetRequiredService<RunBook>(), provider, provider.GetService<TimeProvider>() ?? TimeProvider.System, provider.GetRequiredService<ILogger<RunRecorder>>()));
        services.AddHostedService(provider => provider.GetRequiredService<RunRecorder>());
        services.AddSingleton<AccountRecorder>();
        services.AddSingleton<IGameObserver>(provider => provider.GetRequiredService<AccountRecorder>());
        services.AddHostedService(provider => provider.GetRequiredService<AccountRecorder>());
        services.AddHostedService<StartOnConnection>();
        services.AddSingleton<IHostPreparation, StoragePreparation>();
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();
        bot.AddModule<BehaviorsModule>();

        Register(bot.Services, new StorageSettings { Path = Path.GetFullPath(bot.Configuration[StorageSettings.PathKey] ?? StorageSettings.DefaultPath) });
    }
}
