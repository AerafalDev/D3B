using D3B.Bot.Fights;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;

namespace D3B.Bot.Tests.Fights;

/// <summary>A fight's strategy (D233, D234): the AI's weights it leads to, what it leaves out, and who plays a fight.</summary>
public sealed class StrategiesTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public void StandardFightsWithTheWeightsMeasuredOnTheCaptures()
    {
        Assert.Equal(TacticWeights.Default, Strategies.WeightsOf(new StrategySettings(), risky: false, life: 1));
        Assert.Empty(Strategies.LeftOut(new StrategySettings()));
        Assert.Equal(TacticPlacement.Judged, Strategies.PlacementOf(new StrategySettings()));
    }

    [Fact]
    public void ARiskyFightOrALowLifeIsFoughtPrudentlyAsTheStrategySays()
    {
        var standard = new StrategySettings();
        var careless = new StrategySettings { PrudentWhenRisky = false };
        var underAThird = new StrategySettings { PrudentUnder = true, PrudentBelow = 30 };

        Assert.Equal(TacticWeights.Prudent, Strategies.WeightsOf(standard, risky: true, life: 1));
        Assert.Equal(TacticWeights.Default, Strategies.WeightsOf(careless, risky: true, life: 1));
        Assert.Equal(TacticWeights.Default, Strategies.WeightsOf(underAThird, risky: false, life: 0.5));
        Assert.Equal(TacticWeights.Prudent, Strategies.WeightsOf(underAThird, risky: false, life: 0.2));
    }

    [Fact]
    public void ARoleChangesItsStylesWeights()
    {
        var healer = Strategies.WeightsOf(new StrategySettings { Role = StrategyRole.Healer }, risky: false, life: 1);
        var protector = Strategies.WeightsOf(new StrategySettings { Role = StrategyRole.Protector, Style = StrategyStyle.Offensive }, risky: false, life: 1);
        var offensive = Strategies.WeightsOf(new StrategySettings { Style = StrategyStyle.Offensive }, risky: false, life: 1);

        Assert.Equal((2, TacticWeights.Default.Approach * 0.6), (healer.Allies, healer.Approach));
        Assert.Equal((1.5, offensive.Survival * 0.6, offensive.Approach * 1.25), (protector.Allies, protector.Survival, protector.Approach));
        Assert.True(offensive.Threat < TacticWeights.Default.Threat);
    }

    [Fact]
    public void ItsSpellsArePreferredOrLeftOutTheWeaponWithThem()
    {
        var strategy = new StrategySettings { Preferred = [12, 13], Excluded = [14], Weapon = false, Placement = StrategyPlacement.Far };

        Assert.Equal([12, 13], Strategies.WeightsOf(strategy, risky: false, life: 1).Preferred.Order());
        Assert.Equal([WeaponBlows.SpellId, 14], Strategies.LeftOut(strategy).Order());
        Assert.Equal(TacticPlacement.FarFromEnemies, Strategies.PlacementOf(strategy));
    }

    [Fact]
    public async Task OutOfAnActivityTheStrategySaysWhetherTheAIPlaysTheHostsSwitchPlayingThemAll()
    {
        var (_, bot) = await _play.EnterAsync(300);

        Assert.False(bot.Fights(new StrategySettings()));
        Assert.True(bot.Fights(new StrategySettings { OutOfActivity = true }));

        bot.FightsAlone = true;

        Assert.True(bot.Fights(new StrategySettings()));
    }

    [Fact]
    public void AHarvestFightsItsFightsUnlessItsProfileLeavesThemToThePlayer()
    {
        var sightings = new HarvestSightings();
        var graphics = new GraphicSkills();
        HarvestActivity Harvest(bool ai)
        {
            return new HarvestActivity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId], AiFights = ai }, _play.World, _play.Tables, _play.Maps, sightings, graphics, _play.Places, new Regrowths(), _play.Time);
        }

        Assert.True(new HarvestSettings().AiFights);
        Assert.True(Harvest(ai: true).FightsItsFights);
        Assert.False(Harvest(ai: false).FightsItsFights);
    }
}
