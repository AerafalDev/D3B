using D3B.Core.Modules;

namespace D3B.Core.Plugins;

/// <summary>A plugin read from its folder: its identity and its root module, not built yet.</summary>
internal sealed class LoadedPlugin
{
    public required PluginInfo Info { get; init; }

    public required IModule Module { get; init; }
}
