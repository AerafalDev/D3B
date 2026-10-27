namespace D3B.Core.Plugins;

/// <summary>The exception thrown when a folder under the plugins directory does not hold a valid plugin.</summary>
/// <remarks>The host does not start without every plugin it was given: a bot missing a feature could misbehave.</remarks>
public sealed class PluginLoadException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PluginLoadException"/> class.</summary>
    public PluginLoadException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PluginLoadException"/> class.</summary>
    /// <param name="message">What is wrong with the plugin.</param>
    public PluginLoadException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PluginLoadException"/> class.</summary>
    /// <param name="message">What is wrong with the plugin.</param>
    /// <param name="innerException">The error that stopped the plugin from loading.</param>
    public PluginLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
