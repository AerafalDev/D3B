using Avalonia.Controls;
using Avalonia.Input;
using D3B.App.ViewModels;

namespace D3B.App.Views;

/// <summary>The bots' cards, in the characters' panel until 7c3: each bot's state and activity, and the commands that start, stop and resume it.</summary>
internal sealed partial class BotsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="BotsView"/> class.</summary>
    public BotsView()
    {
        InitializeComponent();
    }

    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        // A click on a card chooses its character for the run block (D323).
        if (sender is Control { DataContext: BotViewModel bot })
            bot.ChooseCommand.Execute(null);
    }
}
