using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Pages;
using D3B.Bot.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace MyPlugin;

/// <summary>The plugin's page: the host's bots, read each time the app refreshes the page.</summary>
internal sealed partial class MyPluginPageModel : ObservableObject
{
    private readonly Bots? _bots;

    /// <summary>Gets the plugin's texts, in the app's language, which follow it as it changes.</summary>
    public PageTexts Texts { get; }

    /// <summary>Gets the bots' names, in the order the host made them.</summary>
    public ObservableCollection<string> Names { get; } = [];

    /// <summary>Gets how many bots there are, said.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Initializes a new instance of the <see cref="MyPluginPageModel"/> class.</summary>
    /// <param name="context">What the page sees of the app: the host's services, its language, and its refresh.</param>
    public MyPluginPageModel(PageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Texts = context.Texts(MyPluginTexts.Resources);
        _bots = context.Services.GetService<Bots>();

        // Every 250 ms while the page shows, on the UI's thread: the bots' state is a snapshot, read for nothing.
        context.Refreshing += (_, _) => Refresh();
    }

    /// <summary>Reads the bots again.</summary>
    public void Refresh()
    {
        var bots = _bots?.All ?? [];
        var names = bots.Select(bot => bot.State.Name).ToList();

        if (!names.SequenceEqual(Names))
        {
            Names.Clear();

            foreach (var name in names)
                Names.Add(name);
        }

        Summary = Texts.Plural("Bots", bots.Count);
    }
}
