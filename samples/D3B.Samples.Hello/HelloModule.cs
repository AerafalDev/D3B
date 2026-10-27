using D3B.Core.Modules;
using D3B.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Samples.Hello;

/// <summary>The root module of a sample plugin: it greets once the host has started.</summary>
[Plugin("d3b.samples.hello", Name = "Hello", Author = "Aerafal")]
public sealed class HelloModule : IModule
{
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.Services.AddHostedService<Greeter>();
    }
}
