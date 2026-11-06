using D3B.Data.Npcs;

namespace D3B.Data.Tests.Npcs;

public sealed class NpcCatalogTests
{
    private static NpcCatalog Catalog()
    {
        return new NpcCatalog(
            new Npc[] { new(1088, 5001), new(4398, 5002) },
            new NpcText[] { new(36_977, 6001), new(36_979, 6002) },
            new NpcText[] { new(30_637, 7001) });
    }

    [Fact]
    public void AnNpcAReplyAndAQuestionAreFoundByTheirIdAndNameTheirText()
    {
        var catalog = Catalog();

        Assert.True(catalog.TryGetNpc(4398, out var npc));
        Assert.Equal(5002, npc.NameId);
        Assert.True(catalog.TryGetReply(36_979, out var reply));
        Assert.Equal(6002, reply.TextId);
        Assert.True(catalog.TryGetQuestion(30_637, out var question));
        Assert.Equal(7001, question.TextId);
    }

    [Fact]
    public void WhatTheCatalogLacksIsNotFound()
    {
        var catalog = Catalog();

        Assert.False(catalog.TryGetNpc(1, out _));
        Assert.False(catalog.TryGetReply(36_978, out _));
        Assert.False(catalog.TryGetQuestion(30_638, out _));
    }

    [Fact]
    public void ATableOutOfOrderOrWithARepeatedIdIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new NpcCatalog(new Npc[] { new(2, 1), new(1, 1) }, Array.Empty<NpcText>(), Array.Empty<NpcText>()));
        Assert.Throws<ArgumentException>(() => new NpcCatalog(Array.Empty<Npc>(), new NpcText[] { new(1, 1), new(1, 2) }, Array.Empty<NpcText>()));
        Assert.Throws<ArgumentException>(() => new NpcCatalog(Array.Empty<Npc>(), Array.Empty<NpcText>(), new NpcText[] { new(3, 1), new(2, 1) }));
    }
}
