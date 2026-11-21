using D3B.Bot.Characters;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Localization;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestReporterTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public void ATourThatWentRoundIsWarnedOfOnTheCardUntilTheNextHarvest()
    {
        // D486: the tour gave up, and the card says why until something is harvested again.
        var settings = new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] };
        var zone = HarvestZone.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Jobs, _play.Maps);
        var current = new HarvestRotationZone(null, zone, new HarvestTour(new HarvestSightings(), new GraphicSkills()));
        var reporter = new HarvestReporter(settings, new HarvestUpkeep(settings, _play.Tables, (_, _) => { }));
        var state = new CharacterState { Key = TestPlay.Key, Name = "Character", MapId = TestPlay.MapId };
        var warning = GameText.Of(HarvestTexts.Text("WarningGoneRound"), GameTextValue.Number(1), GameTextValue.Map(TestPlay.MapId));
        var doing = GameText.Of(HarvestTexts.Text("DoingTravel"));

        reporter.RoundWarning = warning;
        reporter.Publish(current, state, doing);

        Assert.Contains(warning, reporter.Report!.Warnings);

        reporter.Harvested(TestPlay.Ash, 3);
        reporter.Publish(current, state, doing);

        Assert.DoesNotContain(warning, reporter.Report!.Warnings);
    }
}
