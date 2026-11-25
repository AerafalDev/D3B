using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.AppSettings;
using D3B.App.ViewModels.Statistics;
using D3B.App.ViewModels.Studio;
using D3B.Bot.Hosting;

namespace D3B.App.Tests.ViewModels.Studio;

[UiThread]
public sealed class SettingsAndStatisticsTests
{
    private static MainViewModel Studio()
    {
        return new MainViewModel(new HostRunner(_ => new TaskCompletionSource<BotHostStart>().Task), new Localizer(Localizer.English));
    }

    [Fact]
    public void TheSettingsOpenOnGeneralThenBotsFilesAdvancedAndAbout()
    {
        var settings = Studio().Settings;

        Assert.Equal([SettingsSection.General, SettingsSection.Bots, SettingsSection.Files, SettingsSection.Advanced, SettingsSection.About], settings.Sections.Select(section => section.Section));
        Assert.Equal(["General", "Bots", "Files", "Advanced", "About"], settings.Sections.Select(section => section.Title));
        Assert.Equal(SettingsSection.General, settings.Selected.Section);
    }

    [Fact]
    public void TheCapturesAndTheAdvancedToolsAreAdvancedAndTheLanguageGeneral()
    {
        var settings = Studio().Settings;

        settings.Filter = "advanced tools";

        Assert.Equal([SettingsSection.Advanced], settings.Sections.Where(section => section.IsShown).Select(section => section.Section));

        settings.Filter = "record";

        Assert.Equal([SettingsSection.Advanced], settings.Sections.Where(section => section.IsShown).Select(section => section.Section));

        settings.Filter = "language";

        Assert.Contains(SettingsSection.General, settings.Sections.Where(section => section.IsShown).Select(section => section.Section));
        Assert.True(settings.Language.IsShown);
        Assert.False(settings.Advanced.IsShown);
    }

    [Fact]
    public void TheStatisticsAreATabOfTheirOwnWhichTheActivityBarOpensAndSays()
    {
        var main = Studio();

        main.ShowStatistics();

        Assert.IsType<StatisticsViewModel>(main.Editor.Selected?.Content);
        Assert.Equal("Statistics", main.Editor.Selected?.Title);
        Assert.True(main.IsStatisticsShown);
        Assert.False(main.Layout.IsBottomOpen);

        main.ShowHome();

        Assert.False(main.IsStatisticsShown);
        Assert.DoesNotContain(Enum.GetNames<BottomTab>(), name => name == "Statistics");
    }

    [Fact]
    public void ASettingFoundOrTheCapturesNoticeOpensItsSection()
    {
        var main = Studio();

        void Find(string title)
        {
            main.Search.Open(SearchScope.Settings);
            main.Search.Query = title;
            main.Search.Run(main.Search.Groups.SelectMany(group => group.Results).First(result => result.Item.Title == title));
        }

        Find("Record the captures");

        Assert.Equal((SettingsSection.Advanced, "settings"), (main.Settings.Selected.Section, main.Editor.Selected?.Key));

        Find("Language");

        Assert.Equal(SettingsSection.General, main.Settings.Selected.Section);

        main.Home.Notice!.OpenSettingsCommand.Execute(null);

        Assert.Equal(SettingsSection.Advanced, main.Settings.Selected.Section);
    }

    [Fact]
    public void TheRecordedSessionsTitleGoesWhenTheSearchKeepsNoneOfItsRows()
    {
        var settings = Studio().Settings;

        settings.Filter = "advanced tools";

        Assert.Equal((false, true), (settings.IsCapturesShown, settings.AdvancedToolsRow.IsShown));
    }
}
