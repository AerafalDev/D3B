using System.Resources;

namespace D3B.Samples.Hello;

/// <summary>The plugin's texts: <c>Strings.resx</c> in English, and <c>Strings.fr.resx</c> in French, a satellite assembly in the plugin's folder.</summary>
internal static class HelloTexts
{
    /// <summary>Gets the plugin's resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Samples.Hello.Strings", typeof(HelloTexts).Assembly);
}
