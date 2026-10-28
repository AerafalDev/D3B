using D3B.Bot.Data;
using D3B.Bot.Network;
using D3B.Core.Modules;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>The bots' run time: one actor per character a game connection chose, fed by the game it observes (D9, D65).</summary>
/// <remarks>
/// Registers <see cref="Bots"/>, which every module reaches its bots through, as an <see cref="IGameObserver"/> of the
/// network module's game sessions. Requires the data module, whose maps behaviors walk, and the network module.
/// </remarks>
public sealed class BotsModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<DataModule>();
        bot.AddModule<NetworkModule>();

        bot.Services.AddSingleton(services => new Bots(
            services.GetRequiredService<GameMaps>(),
            services.GetRequiredService<GameWorld>(),
            services.GetRequiredService<GameTables>(),
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILoggerFactory>()));
        bot.Services.AddSingleton<IGameObserver>(services => services.GetRequiredService<Bots>());
    }
}
