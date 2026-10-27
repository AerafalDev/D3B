using D3B.Core.Modules;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Releases;
using D3B.Data.Texts;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Data;

/// <summary>The game data of the installed client's build: its pack, opened once, and the stores that read it (D31).</summary>
/// <remarks>
/// <para>
/// Registers <see cref="ClientBuild"/>, <see cref="PackSource"/>, <see cref="ReleaseDownloader"/>,
/// <see cref="GamePack"/>, <see cref="GameTexts"/>, <see cref="GameMaps"/>, <see cref="GameWorld"/> and
/// <see cref="GameTables"/>, every one a singleton that all bots share: the stores are safe for any number of threads.
/// The pack is opened when first asked for and closed when the host is disposed.
/// </para>
/// <para>
/// Nothing is read or downloaded while the module builds. The pack must be on this machine by the time it is opened:
/// <see cref="PackSource.EnsureAsync"/> downloads it when it is not, and the host calls it, then opens the pack, before
/// it starts.
/// </para>
/// <para>
/// The configuration names the client's directory (<c>Client:Directory</c>), where packs are kept
/// (<c>Data:Directory</c>), the repository whose releases ship them (<c>Releases:Repository</c>) and the token their
/// download sends (<c>Releases:Token</c>).
/// </para>
/// </remarks>
public sealed class DataModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var settings = DataSettings.Read(bot.Configuration);

        bot.Services.AddSingleton(_ => new ReleaseDownloader(settings.Repository));
        bot.Services.AddSingleton(services => new PackSource(
            settings,
            services.GetRequiredService<ReleaseDownloader>(),
            new ReleaseToken(settings.Token),
            services.GetRequiredService<ILogger<PackSource>>()));
        bot.Services.AddSingleton(services => services.GetRequiredService<PackSource>().Build);

        // Made by a factory, so that the container owns the pack and closes it with the host.
        bot.Services.AddSingleton(services => services.GetRequiredService<PackSource>().Open());
        bot.Services.AddSingleton(services => new GameTexts(services.GetRequiredService<GamePack>()));
        bot.Services.AddSingleton(services => new GameMaps(services.GetRequiredService<GamePack>()));
        bot.Services.AddSingleton(services => new GameWorld(services.GetRequiredService<GamePack>()));
        bot.Services.AddSingleton(services => new GameTables(services.GetRequiredService<GamePack>()));
    }
}
