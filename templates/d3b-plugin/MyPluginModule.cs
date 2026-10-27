using D3B.Core.Modules;
using D3B.Core.Plugins;

namespace MyPlugin;

/// <summary>The plugin's root module: the host adds it, and it adds the rest of the plugin.</summary>
[Plugin("com.example.myplugin", Name = "MyPlugin")]
public sealed class MyPluginModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        // Add the modules this plugin requires first, with bot.AddModule<T>(), then register what it brings
        // through bot.Services.
    }
}
