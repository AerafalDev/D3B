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
/// <c>Network:Ports</c> (5555 and 443). While the host runs, the <c>ConfigUrl</c> of the client in <c>Client:Directory</c>
/// names a copy of its configuration whose connection host is D3B's address, kept in <c>Network:Redirect:Directory</c>
/// (<c>%LOCALAPPDATA%\D3B\client</c> by default); the client gets its own back when the host stops. Each connection goes
/// to Ankama's connection host, bytes as they are, both ways. For now only the connection phase comes through D3B: the
/// game connection still goes straight to the game server.
/// </para>
/// <para>
/// Requires the protocol module: each frame of the connection phase is decoded through the installed build's translation
/// and logged by what it is, never by what it holds (D49).
/// </para>
/// <para>
/// With <c>Network:Captures:Enabled</c>, each connection is recorded in <c>Network:Captures:Directory</c>
/// (<c>%LOCALAPPDATA%\D3B\captures</c> by default), with the installed client's version when the data module knows it.
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

        bot.Services.AddSingleton<IRelayObserver>(services => new ConnectionPhaseDecoder(
            services.GetRequiredService<ProtocolTranslation>(),
            services.GetRequiredService<ILogger<ConnectionPhaseDecoder>>()));
        bot.Services.AddRelay(settings, services => services.GetService<ClientBuild>()?.Version);
    }
}
