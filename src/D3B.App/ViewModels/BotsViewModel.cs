using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The bots page: every bot of the host, in the order they came, each shown whole (D87).</summary>
internal sealed partial class BotsViewModel : ObservableObject
{
    private readonly Dictionary<CharacterKey, BotViewModel> _byKey = [];
    private readonly Localizer _localizer;
    private readonly Action<CharacterKey>? _showJournal;
    private readonly Action<CharacterKey>? _showMap;

    /// <summary>Gets the bots, in the order the host made them.</summary>
    public ObservableCollection<BotViewModel> Items { get; } = [];

    /// <summary>Gets whether no bot has been made yet.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets how many bots there are, and how many a client plays.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    /// <summary>Initializes a new instance of the <see cref="BotsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="showJournal">Shows a bot's journal on the logs page, as its card asks, or <see langword="null"/> when no page shows it.</param>
    /// <param name="showMap">Shows a bot's map on the map page, as its card asks, or <see langword="null"/> when no page shows it.</param>
    public BotsViewModel(Localizer localizer, Action<CharacterKey>? showJournal = null, Action<CharacterKey>? showMap = null)
    {
        _localizer = localizer;
        _showJournal = showJournal;
        _showMap = showMap;
    }

    /// <summary>Reads the bots again: those the host made since are added, and every one is read.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>: the bots shown stay as they were last read.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services?.GetService<Bots>() is { } bots)
        {
            // Two bots made between two refreshes come in a stable order.
            foreach (var bot in bots.All.OrderBy(bot => bot.Key.AccountId).ThenBy(bot => bot.Key.CharacterId))
            {
                if (_byKey.ContainsKey(bot.Key))
                    continue;

                var item = new BotViewModel(bot, services, _localizer, _showJournal, _showMap);

                _byKey.Add(bot.Key, item);
                Items.Add(item);
            }

            foreach (var item in Items)
                item.Refresh();
        }

        IsEmpty = Items.Count == 0;
        Summary = _localizer.Format("BotsSummary", _localizer.Plural("Bots", Items.Count), _localizer.Plural("Connected", Items.Count(item => item.IsConnected)));
    }
}
