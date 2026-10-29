using D3B.Bot.Activities;
using D3B.Bot.Localization;
#if (Page)
using D3B.Bot.Pages;
#endif
using D3B.Core.Modules;
using D3B.Core.Plugins;

namespace MyPlugin;

/// <summary>The plugin's root module: the host adds it, and it adds the rest of the plugin.</summary>
[Plugin("com.example.myplugin", Name = "MyPlugin")]
public sealed class MyPluginModule : IModule
{
#if (Page)
    /// <summary>The page's icon in the app's rail: lines on a grid of 24 units, in the path syntax of SVG.</summary>
    private const string PageIcon = "M12 3 L20 7.5 V16.5 L12 21 L4 16.5 V7.5 Z M4 7.5 L12 12 L20 7.5 M12 12 V21";

#endif
    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        // Add the modules this plugin requires first, with bot.AddModule<T>(), then register what it brings
        // through bot.Services: its services, its activities, which the app starts on a bot, and its pages.
        bot.Services.AddActivity<MyPluginActivity>("com.example.myplugin.activity", LocalizedText.From(MyPluginTexts.Resources, "ActivityName"));
#if (Page)

        // The page's types are named in its factory alone: a host without the app, which lacks Avalonia, builds this
        // module and never loads them.
        bot.Services.AddPage(
            "com.example.myplugin",
            LocalizedText.From(MyPluginTexts.Resources, "PageTitle"),
            PageIcon,
            context => new MyPluginPage(new MyPluginPageModel(context)));
#endif
    }
}
