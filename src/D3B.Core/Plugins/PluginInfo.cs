namespace D3B.Core.Plugins;

/// <summary>A loaded plugin, as the host registers it in its services.</summary>
public sealed class PluginInfo
{
    /// <summary>Gets the plugin's unique identifier, from its <see cref="PluginAttribute"/>.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the plugin's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the plugin's version: its assembly's informational version.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the plugin's author, when it declares one.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the folder the plugin was loaded from.</summary>
    public required string Directory { get; init; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{Name} {Version} [{Id}]";
    }
}
