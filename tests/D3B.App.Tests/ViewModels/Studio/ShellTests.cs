using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.AppSettings;
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
    public void TheJournalOpensTheBottomPanelOnItsTabAndChosenAgainHidesIt()
    {
        var main = Studio();

        main.ToggleJournal();

        Assert.Equal((true, BottomTab.Journal, true), (main.Layout.IsBottomOpen, main.Bottom.Tab, main.Bottom.IsJournalOpen));

        main.Bottom.Show(BottomTab.Trace);

        Assert.False(main.Bottom.IsJournalOpen);

        main.ToggleJournal();
        main.ToggleJournal();

        Assert.Equal((false, false), (main.Layout.IsBottomOpen, main.Bottom.IsJournalOpen));
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
    public void TheSearchsCommandsShowAndHideTheSideBarTheDetailsAndTheAdvancedToolsAndShowTheStatistics()
    {
        var main = Studio();

        void Run(string command)
        {
            main.Search.Open(SearchScope.Commands);
            main.Search.Query = command;
            main.Search.Run(main.Search.Groups.SelectMany(group => group.Results).First(result => result.Item.Title == command));
        }

        Run("Show or hide the side bar");
        Run("Show or hide the Details");
        Run("Show or hide the advanced tools");
        Run("Show the statistics");

        Assert.Equal((false, true, true), (main.Layout.IsSideOpen, main.Layout.IsDetailsOpen, main.Layout.AdvancedTools));
        Assert.True(main.IsStatisticsShown);
    }

    [Fact]
    public void TheSettingsInterfaceTurnsTheAdvancedToolsOnAndItsSearchFindsThem()
    {
        var main = Studio();

        main.Settings.Studio!.ToggleAdvancedToolsCommand.Execute(null);

        Assert.True(main.Bottom.HasProtocol);

        main.Settings.Filter = "advanced";

        Assert.Equal([SettingsSection.Advanced], main.Settings.Sections.Where(section => section.IsShown).Select(section => section.Section));
        Assert.True(main.Settings.AdvancedToolsRow.IsShown);
        Assert.False(main.Settings.Language.IsShown);
    }

    [Fact]
    public void GoingToTheAssetsShowsTheLibraryEvenWhenTheSideBarWasHidden()
    {
        var main = Studio();

        main.Layout.Close(StudioPanel.Side);
        main.GoToAssets();

        Assert.Equal((true, SideView.Library), (main.Layout.IsSideOpen, main.Layout.View));
    }

    [Fact]
    public void TheStatusBarsItemsOpenWhatTheyName()
    {
        var main = Studio();

        main.StatusBar.ShowProblemsTabCommand.Execute(null);

        Assert.Equal((true, BottomTab.Problems), (main.Layout.IsBottomOpen, main.Bottom.Tab));

        main.StatusBar.ShowRelayJournalCommand.Execute(null);

        Assert.Equal(BottomTab.Journal, main.Bottom.Tab);

        main.Layout.Close(StudioPanel.Side);
        main.StatusBar.ShowCharactersViewCommand.Execute(null);

        Assert.Equal((true, SideView.Characters), (main.Layout.IsSideOpen, main.Layout.View));

        main.StatusBar.ShowDataFilesCommand.Execute(null);

        Assert.Equal((true, SettingsSection.Files), (main.Editor.IsShown("settings"), main.Settings.Sections.Single(section => section.IsSelected).Section));

        main.StatusBar.ShowAboutPageCommand.Execute(null);

        Assert.Equal(SettingsSection.About, main.Settings.Sections.Single(section => section.IsSelected).Section);
    }

    [Fact]
    public void TheStatusBarSaysTheHostStartingAndTheBuildTheAppShips()
    {
        var main = Studio();

        main.Refresh();

        Assert.Equal(("Relay starting…", false), (main.StatusBar.RelayText, main.StatusBar.ShowsPlaying));
        Assert.Equal($"Client {main.Updates.ClientVersion}", main.StatusBar.ClientText);
        Assert.Equal($"D3B {main.Updates.Version}", main.StatusBar.VersionText);
    }
}
