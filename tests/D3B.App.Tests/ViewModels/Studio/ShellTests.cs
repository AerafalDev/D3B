using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Studio;
using D3B.App.ViewModels.World;
using D3B.Bot.Characters;
using D3B.Bot.Hosting;

namespace D3B.App.Tests.ViewModels.Studio;

[UiThread]
public sealed class ShellTests
{
    private static MainViewModel Studio()
    {
        return new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), new Localizer(Localizer.English));
    }

    [Fact]
    public void TheJournalAndTheStatisticsOpenTheBottomPanelOnTheirTabAndChosenAgainHideIt()
    {
        var main = Studio();

        main.ToggleJournal();

        Assert.Equal((true, BottomTab.Journal, true, false), (main.Layout.IsBottomOpen, main.Bottom.Tab, main.Bottom.IsJournalOpen, main.Bottom.IsStatisticsOpen));

        main.ToggleStatistics();

        Assert.Equal((true, BottomTab.Statistics, false, true), (main.Layout.IsBottomOpen, main.Bottom.Tab, main.Bottom.IsJournalOpen, main.Bottom.IsStatisticsOpen));

        main.ToggleStatistics();

        Assert.Equal((false, false), (main.Layout.IsBottomOpen, main.Bottom.IsStatisticsOpen));
    }

    [Fact]
    public void TheProtocolShowsOnlyWithTheAdvancedTools()
    {
        var main = Studio();

        Assert.False(main.Bottom.HasProtocol);

        main.Layout.ToggleAdvancedTools();
        main.Bottom.Show(BottomTab.Protocol);

        Assert.Equal((true, BottomTab.Protocol), (main.Bottom.HasProtocol, main.Bottom.Tab));

        // Hidden while it shows, the protocol leaves the journal in its place.
        main.Layout.ToggleAdvancedTools();

        Assert.Equal((false, BottomTab.Journal), (main.Bottom.HasProtocol, main.Bottom.Tab));
    }

    [Fact]
    public void SelectingSomethingShowsTheDetailsWhichStayHiddenOnceClosedWhileItStaysSelected()
    {
        var main = Studio();

        Assert.False(main.Layout.IsDetailsOpen);

        main.Selection.SelectCharacter(new CharacterKey(1, 2));

        Assert.True(main.Layout.IsDetailsOpen);

        main.Layout.Close(StudioPanel.Details);
        main.Selection.SelectCharacter(new CharacterKey(1, 2));

        Assert.False(main.Layout.IsDetailsOpen);

        main.Selection.SelectZone("forest");

        Assert.True(main.Layout.IsDetailsOpen);

        main.Layout.Close(StudioPanel.Details);
        main.Selection.SelectRoute("north", 1);

        Assert.True(main.Layout.IsDetailsOpen);

        main.Layout.Close(StudioPanel.Details);
        main.Selection.SelectRoute("north", 2);

        // Another step of the route selected is the same thing to edit: the Details stay as the hand left them.
        Assert.False(main.Layout.IsDetailsOpen);

        main.Selection.Clear();
        main.Selection.SelectRoute("north", 2);

        Assert.True(main.Layout.IsDetailsOpen);
    }

    [Fact]
    public void TheOutlinerListsWhatTheSideBarsViewNames()
    {
        var main = Studio();

        Assert.Equal(OutlinerScope.Characters, main.Outliner.Scope);

        main.Layout.ShowView(SideView.World);

        Assert.Equal(OutlinerScope.World, main.Outliner.Scope);

        main.Layout.ShowView(SideView.Library);

        Assert.Equal(OutlinerScope.World, main.Outliner.Scope);

        main.Layout.ShowView(SideView.Characters);

        Assert.Equal(OutlinerScope.Characters, main.Outliner.Scope);
    }

    [Fact]
    public void GoingToTheAssetsShowsTheLibraryEvenWhenTheSideBarWasHidden()
    {
        var main = Studio();

        main.Layout.Close(StudioPanel.Side);
        main.GoToAssets();

        Assert.Equal((true, SideView.Library), (main.Layout.IsSideOpen, main.Layout.View));
    }
}
