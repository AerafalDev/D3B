using D3B.Bot.Activities;

namespace D3B.Bot.Tests.Activities;

public sealed class PlaceRotationTests
{
    private static readonly RotationCandidate[] _twelve =
    [
        .. Enumerable.Range(1, 12).Select(id => new RotationCandidate(id, 100 - id)),
    ];

    private static bool Anywhere(int subArea)
    {
        return true;
    }

    [Fact]
    public void ARotationOfOneIsTheRichestReachableAlone()
    {
        Assert.Equal([2], PlaceRotation.Draw(_twelve, [7, 8], 1, subArea => subArea != 1, new Random(1)));
    }

    [Fact]
    public void AnUnreachableSubAreaIsPassedOverAndTheNextOfTheRankingTakesItsPlace()
    {
        var drawn = PlaceRotation.Draw(_twelve, [], 10, subArea => subArea > 3, new Random(1));

        // The pool is the ten richest reachable: sub-areas 4 to 13 would be, and the ranking stops at 12.
        Assert.Equal(Enumerable.Range(4, 9), drawn.Order());
    }

    [Fact]
    public void ASubAreaWorthUnderAQuarterOfTheRichestIsLeftOut()
    {
        RotationCandidate[] ranked = [new(1, 100), new(2, 60), new(3, 25), new(4, 24.9), new(5, 1)];

        var drawn = PlaceRotation.Draw(ranked, [], 5, Anywhere, new Random(1));

        Assert.Equal([1, 2, 3], drawn.Order());
    }

    [Fact]
    public void TheGivenSubAreasStayEvenOutsideThePoolButHalfAtMostRoundedUp()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var drawn = PlaceRotation.Draw(_twelve, [12, 11, 10, 9], 5, Anywhere, new Random(seed));

            // Three of five are given, 12 and 11 beyond the pool of ten, and two drawn from it, the given ones apart.
            Assert.Equal(5, drawn.Count);
            Assert.Equal([12, 11, 10], drawn.Take(3));
            Assert.Equal(5, drawn.Distinct().Count());
            Assert.All(drawn.Skip(3), subArea => Assert.InRange(subArea, 1, 9));
        }
    }

    [Fact]
    public void AGivenSubAreaTheCharacterCannotReachIsNotKept()
    {
        var drawn = PlaceRotation.Draw(_twelve, [5, 6], 4, subArea => subArea != 5, new Random(3));

        Assert.Equal(6, drawn[0]);
        Assert.DoesNotContain(5, drawn);
    }

    [Fact]
    public void TheOthersAreDrawnAtRandomTheRicherLikelierAndNoneIgnored()
    {
        RotationCandidate[] ranked = [new(1, 100), new(2, 90), new(3, 80), new(4, 70), new(5, 60), new(6, 30)];
        var counts = new Dictionary<int, int>();
        var random = new Random(7);

        for (var round = 0; round < 2_000; round++)
        {
            foreach (var subArea in PlaceRotation.Draw(ranked, [], 3, Anywhere, random))
                counts[subArea] = counts.GetValueOrDefault(subArea) + 1;
        }

        Assert.Equal([1, 2, 3, 4, 5, 6], counts.Keys.Order());
        Assert.True(counts[1] > counts[6]);
        Assert.All(counts.Values, count => Assert.True(count > 100));
    }

    [Fact]
    public void ARotationHoldsFewerWhenThePoolDoes()
    {
        Assert.Equal([1, 2], PlaceRotation.Draw([new(1, 10), new(2, 9)], [], 5, Anywhere, new Random(1)).Order());
        Assert.Empty(PlaceRotation.Draw(_twelve, [1], 5, _ => false, new Random(1)));
        Assert.Empty(PlaceRotation.Draw([], [1], 5, Anywhere, new Random(1)));
    }

    [Fact]
    public void SubAreasOfNoWorthAreDrawnAtRandomRatherThanNeverOrAlways()
    {
        var drawn = PlaceRotation.Draw([new(1, 0), new(2, 0), new(3, 0)], [], 2, Anywhere, new Random(2));

        Assert.Equal(2, drawn.Distinct().Count());
    }
}
