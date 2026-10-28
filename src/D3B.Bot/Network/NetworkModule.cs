using D3B.Bot.Data;
using D3B.Bot.Protocol;
using D3B.Core.Modules;
using D3B.Data;
using D3B.Network.Relay;
using D3B.Protocol.Translation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>The relay between each official client and Ankama's servers, on D3B's loopback address (D35, D36).</summary>
/// <remarks>
/// <para>
/// Registers Kestrel, started and stopped with the host, listening on <c>Network:Address</c> (127.0.13.59 by default) on
/// <c>Network:Ports</c> (5555 and 443) for the connection phase, and on <c>Network:Game:Ports</c> (5556) for the game.
/// While the host runs, the <c>ConfigUrl</c> of the client in <c>Client:Directory</c> names a copy of its configuration
/// whose connection host is D3B's address, kept in <c>Network:Redirect:Directory</c> (<c>%LOCALAPPDATA%\D3B\client</c> by
/// default); the client gets its own back when the host stops. Each connection phase goes to Ankama's connection host,
/// bytes as they are, both ways, but for the server selection's answer, which sends the client's game connection to D3B;
/// that connection goes to the game server whose ticket it presents (D51 to D53).
/// </para>
/// <para>
/// Requires the protocol module: each frame of the connection phase is decoded through the installed build's translation
/// and logged by what it is, never by what it holds (D49). The game connection is relayed unread.
/// </para>
/// <para>
/// With <c>Network:Captures:Enabled</c>, each connection is recorded in <c>Network:Captures:Directory</c>
/// (<c>%LOCALAPPDATA%\D3B\captures</c> by default), the game's in its <c>game</c> directory, with the installed client's
/// version when the data module knows it.
/// </para>
/// </remarks>
public sealed class NetworkModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<ProtocolModule>();

        var settings = RelaySettings.Read(bot.Configuration, DataSettings.ClientDirectoryIn(bot.Configuration));

        bot.Services.AddSingleton<IRelayInterceptor>(services => new ConnectionPhaseDecoder(
            services.GetRequiredService<ProtocolTranslation>(),
            services.GetRequiredService<GameServerSelections>(),
            services.GetRequiredService<ILogger<ConnectionPhaseDecoder>>()));
        bot.Services.AddRelay(settings, services => services.GetService<ClientBuild>()?.Version);
    }
}
