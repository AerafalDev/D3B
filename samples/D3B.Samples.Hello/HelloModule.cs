using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Pages;
using D3B.Core.Modules;
using D3B.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Samples.Hello;

/// <summary>The root module of a sample plugin: it greets once the host has started, brings a stroll for the bots, and a page that shows it.</summary>
[Plugin("d3b.samples.hello", Name = "Hello", Author = "Aerafal")]
public sealed class HelloModule : IModule
{
    /// <summary>The page's icon in the app's rail: a speech bubble, on the app's grid of 24 units.</summary>
    private const string PageIcon = "M5 4.5 H19 A2 2 0 0 1 21 6.5 V14.5 A2 2 0 0 1 19 16.5 H10 L6 20 V16.5 H5 A2 2 0 0 1 3 14.5 V6.5 A2 2 0 0 1 5 4.5 Z"
        + " M8 10.5 H8.01 M12 10.5 H12.01 M16 10.5 H16.01";

    private static LocalizedText Text(string key)
    {
        return LocalizedText.From(HelloTexts.Resources, key);
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.Services.AddHostedService<Greeter>();
        bot.Services.AddSingleton<StrollJournal>();
        bot.Services.AddActivity<Stroll, StrollSettings>("d3b.samples.hello.stroll", Text("StrollName"), settings =>
        {
            settings.Number(s => s.Walks, Text("StrollWalks"), 1, 100).Help(Text("StrollWalksHelp"));
            settings.Number(s => s.Reach, Text("StrollReach"), 1, 8).Help(Text("StrollReachHelp"));
        });

        // The page's types are named in its factory alone: a host without the app, which lacks Avalonia, builds this
        // module and never loads them.
        bot.Services.AddPage(
            "d3b.samples.hello",
            Text("PageTitle"),
            PageIcon,
            context => new HelloPage(new HelloPageModel(context)));
    }
}
