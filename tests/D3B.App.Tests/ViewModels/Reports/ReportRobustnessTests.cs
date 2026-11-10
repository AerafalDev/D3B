using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Reports;
using D3B.Bot.Reports;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Network.Relay;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels.Reports;

[UiThread]
public sealed class ReportRobustnessTests : IAsyncDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly Localizer _localizer = new(Localizer.English);
    private readonly TestPlay _play = new();

    public async ValueTask DisposeAsync()
    {
        await _play.DisposeAsync();
        _directory.Dispose();
    }

    private ReportSources AppSources(params ReportPart[] assets)
    {
        return new ReportSources { CharacterId = 0, CharacterName = "", Assets = assets, App = "0.8.1", Client = "3.7.13.17", Folder = _directory.Combine("reports") };
    }

    private ReportDialogViewModel Dialog(Func<ReportCharacterChoice?, ReportSources?> sources)
    {
        return new ReportDialogViewModel(_localizer, new Dialogs(), [new ReportCharacterChoice(1, 11, "Aldebarane", "")], sources, _ => { }, _ => { });
    }

    [Fact]
    public async Task AnythingThatFailsWhilePreparingGivesTheFormBackWithWhatFailed()
    {
        foreach (Exception failure in new Exception[] { new InvalidDataException("corrupt"), new IOException("locked"), new InvalidCastException("odd") })
        {
            var dialog = Dialog(_ => throw failure);

            dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);

            await dialog.NextCommand.ExecuteAsync(null);

            Assert.True(dialog.IsForm);
            Assert.Contains(failure.Message, dialog.Problem, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AnFailureOfTheKnowledgeReadAlsoGivesTheFormBack()
    {
        var dialog = Dialog(_ => AppSources() with { Knowledge = _ => throw new InvalidDataException("database") });

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);

        await dialog.NextCommand.ExecuteAsync(null);

        Assert.True(dialog.IsForm);
        Assert.Contains("database", dialog.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ATextPastTheLimitIsRefusedWithAWordAndTheFieldGivenItsTextBack()
    {
        var dialog = Dialog(_ => null);
        var changed = new List<string?>();

        dialog.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        dialog.Message = "short";
        dialog.Message = new string('x', ReportContent.MessageLimit + 500);

        Assert.Equal("short", dialog.Message);
        Assert.True(dialog.HasMessageNotice);
        Assert.Contains("1,500", dialog.MessageNotice, StringComparison.Ordinal);
        Assert.Contains(nameof(ReportDialogViewModel.Message), changed.Skip(1));

        dialog.Message = "shorter";

        Assert.False(dialog.HasMessageNotice);
    }

    [Fact]
    public void WithNoCharacterAReportOfTheGameSaysSoAndOneOfTheAppDoesNot()
    {
        var dialog = new ReportDialogViewModel(_localizer, new Dialogs(), [], _ => null, _ => { }, _ => { });

        Assert.True(dialog.HasNoCharacter);

        dialog.Area = dialog.Areas.Single(area => area.Area is ReportArea.App);

        Assert.False(dialog.HasNoCharacter);
    }

    [Fact]
    public async Task AWriteThatFailsLeavesNoFileAndASecondTryWorks()
    {
        var draft = await ReportDraft.PrepareAsync(AppSources(), ReportArea.App, null, _localizer, TestContext.Current.CancellationToken);
        var target = Path.Combine(_directory.Combine("reports"), draft.Code + ".d3breport");

        // A directory in the report's place: the move cannot be made.
        Directory.CreateDirectory(target);

        Assert.ThrowsAny<IOException>(() => draft.Write());
        Assert.Empty(Directory.GetFiles(_directory.Combine("reports")));

        Directory.Delete(target);

        Assert.True(File.Exists(draft.Write().Path));
    }

    [Fact]
    public async Task TheProfileAndWhatTheBotsLearnedCanBeReadBeforeTheyGo()
    {
        var learned = new ReportPart(ReportFiles.Knowledge, "{\"mapNpcs\":[]}"u8.ToArray());
        var draft = await ReportDraft.PrepareAsync(AppSources(new ReportPart("assets/p1.json", "{\"id\":\"p1\"}"u8.ToArray())) with { Knowledge = _ => Task.FromResult(learned) }, ReportArea.App, null, _localizer, TestContext.Current.CancellationToken);

        Assert.All(draft.Parts.Where(part => part.Files.Any(file => file.Name is "assets/p1.json" or ReportFiles.Knowledge)), part => Assert.True(part.HasPreview));
        Assert.Contains(draft.Parts, part => part.Preview?.Contains("// assets/p1.json", StringComparison.Ordinal) is true);
    }

    [Fact]
    public void AReportOfTheAppNeedsNoHostAndOneOfTheGameDoes()
    {
        var choice = new ReportCharacterChoice(1, 11, "Aldebarane", "");

        Assert.Null(ReportSourcesFactory.Read(null, choice, [choice], null, "0.8.1", _directory.Path));
        Assert.NotNull(ReportSourcesFactory.Read(null, null, [choice], null, "0.8.1", _directory.Path));
    }

    [Fact]
    public async Task TheCaptureOfACharacterIsFoundByTheLastConnectionOfItsBotAndAnotherAccountsSameIdIsAnotherCharacter()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var games = _directory.Combine("captures", "game");

        Directory.CreateDirectory(games);

        var capture = Path.Combine(games, $"20261110-090000-{connection.Session.Id}.d3bcap");

        File.WriteAllBytes(capture, [1]);

        var services = new ServiceCollection()
            .AddSingleton(_play.Bots)
            .AddSingleton(new RelaySettings { Address = RelaySettings.DefaultAddress, Ports = [0], GamePorts = [0], ClientDirectory = "c", RedirectDirectory = "r", CapturesDirectory = _directory.Combine("captures"), RecordsGame = true })
            .BuildServiceProvider();
        var chosen = new ReportCharacterChoice(bot.Key.AccountId, bot.Key.CharacterId, "Aldebarane", "");
        var sameIdElsewhere = new ReportCharacterChoice(bot.Key.AccountId + 1, bot.Key.CharacterId, "Zephyrine", "");

        var sources = ReportSourcesFactory.Read(services, chosen, [chosen, sameIdElsewhere], null, "0.8.1", _directory.Path)!;

        Assert.Equal(capture, sources.CapturePath);
        Assert.Equal(["Zephyrine"], sources.OtherNames);

        connection.Close();
        await bot.ProcessedAsync();

        // The bot is offline now: its last connection's capture is still the one.
        Assert.Equal(capture, ReportSourcesFactory.Read(services, chosen, [chosen], null, "0.8.1", _directory.Path)!.CapturePath);

        // A character no bot plays has none.
        Assert.Null(ReportSourcesFactory.Read(services, new ReportCharacterChoice(9, 9, "Nobody", ""), [], null, "0.8.1", _directory.Path)!.CapturePath);
    }
}
