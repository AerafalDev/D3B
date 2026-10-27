namespace D3B.Core.Plugins;

/// <summary>Marks the root module of a plugin and declares the plugin's identity.</summary>
/// <remarks>
/// A plugin assembly holds exactly one public module class with this attribute. The host adds that module, and the
/// module adds the other modules the plugin brings. The plugin's version is its assembly's.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginAttribute : Attribute
{
    /// <summary>Gets the plugin's unique identifier, such as <c>com.example.harvester</c>.</summary>
    public string Id { get; }

    /// <summary>Gets the plugin's display name. The default is the name of its folder.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the plugin's author.</summary>
    public string? Author { get; init; }

    /// <summary>Initializes a new instance of the <see cref="PluginAttribute"/> class.</summary>
    /// <param name="id">The plugin's unique identifier.</param>
    public PluginAttribute(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
    }
}
