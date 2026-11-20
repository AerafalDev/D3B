using D3B.Engine.Tactics;

namespace D3B.Engine.Tests.Tactics;

/// <summary>The share of next turn's damage the approach counts where no spell lands yet (D201, D471).</summary>
public sealed class JudgeTurnsTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(13)]
    public void EachCellNearerAnEnemyNoSpellReachesCountsALittleMoreUpToItsCell(int reach)
    {
        // Measured on the owner's run's captures (D471): within the spells' range and MP, every cell was worth the same half.
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
}
