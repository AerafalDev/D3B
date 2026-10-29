using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Characters;
using D3B.Bot.Pages;
using D3B.Bot.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Samples.Hello;

/// <summary>The plugin's page: a card for each bot, with what its strolls did.</summary>
/// <remarks>It reads the bots and the journal each time the app refreshes the page, on the UI's thread, as the app's own pages do.</remarks>
internal sealed partial class HelloPageModel : ObservableObject
{
    private readonly Dictionary<CharacterKey, StrollCardModel> _byKey = [];
    private readonly Bots? _bots;
    private readonly StrollJournal _journal;

    /// <summary>Gets the plugin's texts, in the app's language.</summary>
    public PageTexts Texts { get; }

    /// <summary>Gets a card for each bot, in the order the host made them.</summary>
    public ObservableCollection<StrollCardModel> Cards { get; } = [];

    /// <summary>Gets whether no bot has been made yet.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Initializes a new instance of the <see cref="HelloPageModel"/> class.</summary>
    /// <param name="context">What the page sees of the app.</param>
    public HelloPageModel(PageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Texts = context.Texts(HelloTexts.Resources);
        _bots = context.Services.GetService<Bots>();
        _journal = context.Services.GetRequiredService<StrollJournal>();
        context.Refreshing += (_, _) => Refresh();
    }

    /// <summary>Reads the bots and the journal again.</summary>
    public void Refresh()
    {
        foreach (var bot in (_bots?.All ?? []).OrderBy(bot => bot.Key.AccountId).ThenBy(bot => bot.Key.CharacterId))
        {
            if (_byKey.ContainsKey(bot.Key))
                continue;

            var card = new StrollCardModel(bot, _journal, Texts);

            _byKey.Add(bot.Key, card);
            Cards.Add(card);
        }

        foreach (var card in Cards)
            card.Refresh();

        IsEmpty = Cards.Count == 0;
    }
}
