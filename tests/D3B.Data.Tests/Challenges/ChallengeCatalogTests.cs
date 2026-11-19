using D3B.Data.Challenges;

namespace D3B.Data.Tests.Challenges;

public sealed class ChallengeCatalogTests
{
    private static readonly int[] _none = [];
    private static readonly string[] _one = ["TM=1,0"];
    private static readonly string[] _two = ["TM=1,0", "Tm<1,0"];

    [Fact]
    public void AChallengeIsFoundByItsIdWithItsTextsAndCriteria()
    {
        var catalog = TestTables.Challenges();

        Assert.True(catalog.TryGetChallenge(2, out var challenge));
        Assert.Equal(758, challenge.NameId);
        Assert.Equal(1_008_206, challenge.DescriptionId);
        Assert.Equal(1, challenge.CategoryId);
        Assert.Equal("Tm<1,0", catalog.CompletionOf(2));
        Assert.Equal("GL>4,0", catalog.ActivationOf(2));
    }

    [Fact]
    public void AChallengeExcludesItsOwnSlice()
    {
        var catalog = TestTables.Challenges();

        Assert.Equal([2, 8], catalog.IncompatibleWith(1).ToArray());
        Assert.Equal([1], catalog.IncompatibleWith(2).ToArray());
    }

    [Fact]
    public void WhatTheCatalogLacksIsNotFound()
    {
        var catalog = TestTables.Challenges();

        Assert.False(catalog.TryGetChallenge(3, out _));
        Assert.Null(catalog.CompletionOf(3));
        Assert.Null(catalog.ActivationOf(3));
        Assert.True(catalog.IncompatibleWith(3).IsEmpty);
    }

    [Fact]
    public void ATableOutOfOrderOrWithoutItsCriteriaIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ChallengeCatalog(new Challenge[] { new(2, 1, 1, 1, 1, 0, 0, 0), new(1, 1, 1, 1, 1, 0, 0, 0) }, _none, _two, _two));
        Assert.Throws<ArgumentException>(() => new ChallengeCatalog(new Challenge[] { new(1, 1, 1, 1, 1, 0, 0, 0), new(2, 1, 1, 1, 1, 0, 0, 0) }, _none, _one, _two));
        Assert.Throws<ArgumentException>(() => new ChallengeCatalog(new Challenge[] { new(1, 1, 1, 1, 1, 0, 0, 0), new(2, 1, 1, 1, 1, 0, 0, 0) }, _none, _two, _one));
        Assert.Throws<ArgumentException>(() => new ChallengeCatalog(new Challenge[] { new(1, 1, 1, 1, 1, 0, 0, 1) }, _none, _one, _one));
    }
}
