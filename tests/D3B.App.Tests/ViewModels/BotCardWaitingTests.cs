using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Tests.Groups;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.ViewModels;

/// <summary>The bot's card of a master waiting for its followers: it says whom for (D432, D437).</summary>
[UiThread]
public sealed class BotCardWaitingTests
{
    [Fact]
    public async Task AMastersCardSaysWhomItWaitsForThenWhatItDoesAgain()
    {
        await using var play = new TestPlay();
        var (masterConnection, master) = await TestGroup.MasterAsync(play, 300);
        var (followerConnection, follower) = await TestGroup.FollowerAsync(play, 300, TestPlay.NorthMapId);
        using var services = new ServiceCollection().BuildServiceProvider();
        var card = new BotViewModel(master, services, new Localizer(Localizer.English));

        card.Refresh();
        Assert.Equal("", card.ActivityDoing);

        using var membership = play.Bots.Groups.Follow(follower, TestGroup.Master, out _);
        var acting = master.RunAsync(async (context, token) =>
        {
            // An action, which the master waits for its followers before: its map changes no longer (D447).
            await context.SendAsync(new AttackMonsterRequest { MonsterGroupId = -1 }, token);

            return true;
        }, TestContext.Current.CancellationToken);

        await TestGroup.UntilAsync(play, () => master.WaitingFor.Count > 0, "waiting");
        card.Refresh();

        Assert.Equal("Waits for Follower, on its way", card.ActivityDoing);

        masterConnection.Server(TestGroup.Party(TestGroup.PartyId, TestGroup.MasterId, TestGroup.MasterId, TestGroup.FollowerId));
        TestGroup.Arrive(followerConnection, TestGroup.FollowerId, TestPlay.MapId, 301);
        await acting;
        card.Refresh();

        Assert.Equal("", card.ActivityDoing);
    }
}
