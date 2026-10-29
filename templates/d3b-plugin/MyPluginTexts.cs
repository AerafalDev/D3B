using System.Resources;

namespace MyPlugin;

/// <summary>The plugin's texts: <c>Strings.resx</c> in English, and <c>Strings.fr.resx</c> in French, which the app shows in its language.</summary>
internal static class MyPluginTexts
{
    /// <summary>Gets the plugin's resources.</summary>
    public static ResourceManager Resources { get; } = new("MyPlugin.Strings", typeof(MyPluginTexts).Assembly);
}
