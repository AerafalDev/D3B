using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Groups;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Groups;

public sealed class GroupGiftsTests
{
    private const int Unknown = 99_999;

    private static CharacterState State(int weight, params InventoryItem[] stacks)
    {
        return CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character) with
        {
            Weight = weight,
            MaxWeight = 1_000,
            Inventory = stacks.ToImmutableSortedDictionary(stack => stack.Uid, stack => stack),
        };
    }

    [Fact]
    public async Task AFollowerGivesWhatItDoesNotWearAsFarAsItsMastersPodsHold()
    {
        await using var play = new TestPlay();
        var follower = State(0, new InventoryItem(3, TestPlay.WheatBag, 50, InventoryItem.NotWorn), new InventoryItem(1, TestPlay.Ash, 40, InventoryItem.NotWorn), new InventoryItem(2, 2_400, 1, 1));

        // Ash weighs 5 and a wheat bag 1: 150 pods free hold 30 ash; 300 the 40 ash whole and the 50 bags; 240 the ash and 40 bags.
        Assert.Equal([(1, 30)], GroupGifts.Of(follower, State(850), play.Tables.Items, false, new HashSet<int>()));
        Assert.Equal([(1, 40), (3, 50)], GroupGifts.Of(follower, State(700), play.Tables.Items, false, new HashSet<int>()));
        Assert.Equal([(1, 40), (3, 40)], GroupGifts.Of(follower, State(760), play.Tables.Items, false, new HashSet<int>()));
    }

    [Fact]
    public async Task AMasterWhosePodsAreFullIsGivenNothing()
    {
        await using var play = new TestPlay();
        var follower = State(0, new InventoryItem(1, TestPlay.Ash, 40, InventoryItem.NotWorn));

        Assert.Empty(GroupGifts.Of(follower, State(1_000), play.Tables.Items, false, new HashSet<int>()));
        Assert.Empty(GroupGifts.Of(follower, State(1_004), play.Tables.Items, false, new HashSet<int>()));
        Assert.True(GroupGifts.Carries(follower, play.Tables.Items, false, new HashSet<int>()));
    }

    [Fact]
    public async Task AFollowerKeepsTheFoodItEatsAndWhatItWears()
    {
        // D441: the follower eats what only heals before its master acts; the potion, whose use does more, is given.
        await using var play = new TestPlay();
        var follower = State(0, new InventoryItem(1, TestPlay.Bread, 5, InventoryItem.NotWorn), new InventoryItem(2, TestPlay.Ash, 1, 1), new InventoryItem(3, TestPlay.ClassPotion, 1, InventoryItem.NotWorn));

        Assert.Equal([(3, 1)], GroupGifts.Of(follower, State(0), play.Tables.Items, false, new HashSet<int>()));
        Assert.False(GroupGifts.Carries(State(0, new InventoryItem(1, TestPlay.Bread, 5, InventoryItem.NotWorn), new InventoryItem(2, TestPlay.Ash, 1, 1)), play.Tables.Items, false, new HashSet<int>()));
    }

    [Fact]
    public async Task AFollowerKeepsWhatItsMastersProfileKeepsSuchAsADungeonsKey()
    {
        // D529: each member enters a dungeon with its own key or keyring (D528), so neither is given at the group's bank.
        await using var play = new TestPlay();
        var follower = State(0, new InventoryItem(1, TestPlay.Ash, 4, InventoryItem.NotWorn), new InventoryItem(2, TestPlay.DungeonKeyId, 1, InventoryItem.NotWorn));
        var key = State(0, new InventoryItem(2, TestPlay.DungeonKeyId, 1, InventoryItem.NotWorn));

        Assert.Equal([(1, 4)], GroupGifts.Of(follower, State(0), play.Tables.Items, false, new HashSet<int>(), stack => stack.ItemId == TestPlay.DungeonKeyId));
        Assert.False(GroupGifts.Carries(key, play.Tables.Items, false, new HashSet<int>(), stack => stack.ItemId == TestPlay.DungeonKeyId));
        Assert.True(GroupGifts.Carries(key, play.Tables.Items, false, new HashSet<int>()));
    }

    [Fact]
    public async Task AnItemThePackLacksIsGivenWhole()
    {
        await using var play = new TestPlay();
        var follower = State(0, new InventoryItem(1, Unknown, 7, InventoryItem.NotWorn));

        Assert.Equal([(1, 7)], GroupGifts.Of(follower, State(999), play.Tables.Items, false, new HashSet<int>()));
    }

    [Fact]
    public async Task AFollowerGivesItsFoodAsItsMastersProfileBanksAllAndNotTheStacksAnExchangeLeft()
    {
        await using var play = new TestPlay();
        var follower = State(0, new InventoryItem(1, TestPlay.Bread, 5, InventoryItem.NotWorn), new InventoryItem(2, TestPlay.Ash, 1, InventoryItem.NotWorn), new InventoryItem(3, TestPlay.WheatBag, 2, InventoryItem.NotWorn));

        Assert.Equal([(2, 1), (3, 2)], GroupGifts.Of(follower, State(0), play.Tables.Items, false, new HashSet<int>()));
        Assert.Equal([(1, 5), (2, 1), (3, 2)], GroupGifts.Of(follower, State(0), play.Tables.Items, true, new HashSet<int>()));
        Assert.Equal([(1, 5), (3, 2)], GroupGifts.Of(follower, State(0), play.Tables.Items, true, new HashSet<int> { 2 }));
        Assert.False(GroupGifts.Carries(follower, play.Tables.Items, true, new HashSet<int> { 1, 2, 3 }));
    }
}
