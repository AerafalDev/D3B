using D3B.Bot.Hosting;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage;

/// <summary>D3B's database, SQLite through EF Core: the accounts and characters the relay saw, and the profiles (D99, D100).</summary>
/// <remarks>
/// <para>
/// The app and <c>D3B.Host</c> both add it to the host's composition, so both read and write one database, in
/// <c>Storage:Path</c> (<c>%LOCALAPPDATA%\D3B\d3b.db</c> by default). Its migrations apply before the host starts
/// (<see cref="IHostPreparation"/>): a database that does not open stops the host, which says why.
/// </para>
/// <para>
/// It registers the <see cref="AccountBook"/>, fed by the game connections it observes, the <see cref="ProfileBook"/>,
/// the <see cref="ProfileStarter"/> that starts a profile on a bot, and starts a character's profile on its connection,
/// when asked. Plugins do not see it: profiles are D3B's, and only name the activities plugins register.
/// </para>
/// </remarks>
public sealed class StorageModule : IModule
{
    internal static void Register(IServiceCollection services, StorageSettings settings)
    {
        services.AddSingleton(settings);
        services.AddDbContextFactory<StorageContext>(options => options.UseSqlite(settings.ConnectionString));
        services.AddSingleton<AccountBook>();
        services.AddSingleton<ProfileBook>();
        services.AddSingleton<ProfileStarter>();
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

        Register(bot.Services, new StorageSettings { Path = Path.GetFullPath(bot.Configuration[StorageSettings.PathKey] ?? StorageSettings.DefaultPath) });
    }
}
