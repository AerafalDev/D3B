using D3B.Engine.Fights;
using D3B.Engine.Tactics;

namespace D3B.Engine.Tests.Tactics;

/// <summary>The share of next turn's damage the approach counts where no spell lands yet (D201, D472).</summary>
public sealed class JudgeTurnsTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(13)]
    public void EachCellNearerAnEnemyNoSpellReachesCountsALittleMoreUpToItsCell(int reach)
    {
        // Measured on the owner's run's captures (D472): within the spells' range and MP, every cell was worth the same half.
        for (var distance = 1; distance <= reach + 6; distance++)
            Assert.True(JudgeTurns.Nearing(distance, reach) > JudgeTurns.Nearing(distance + 1, reach));
    }

    [Fact]
    public void TheShareIsHalfJustBeyondTheReachAndATenthMoreAtMostWithinIt()
    {
        Assert.Equal(0.5, JudgeTurns.Nearing(10, 9));
        Assert.Equal(0.425, JudgeTurns.Nearing(11, 9), 6);
        Assert.True(JudgeTurns.Nearing(1, 20) is > 0.5 and <= 0.55);
        Assert.True(JudgeTurns.Nearing(0, 20) <= 0.55);
    }

    [Fact]
    public void AShareOrAReductionOfTheDamageReceivedChangesWhatTheJudgesTablesRead()
    {
        // D490: a cast giving a share of 1163 or a reduction of 105 changes the damage its fighter receives in the judge's tables.
        var fighter = new Combatant { Id = 1 };

        Assert.NotEqual(JudgeTurns.Signature(fighter), JudgeTurns.Signature(fighter with { ReceivedDamageShares = [120] }));
        Assert.NotEqual(JudgeTurns.Signature(fighter), JudgeTurns.Signature(fighter with { ReceivedDamageReduction = 8 }));
        Assert.Equal(JudgeTurns.Signature(fighter), JudgeTurns.Signature(fighter with { Life = 10 }));
    }
}
