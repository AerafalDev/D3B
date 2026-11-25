using System.Globalization;
using D3B.Bot.Characters;
using D3B.Bot.Localization;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Groups;

/// <summary>A master and its followers as a test plays them, each its own account and connection (D437).</summary>
internal static class TestGroup
{
    public const long MasterId = TestPlay.CharacterId;
    public const long FollowerId = 80_001;
    public const long SecondId = 90_001;
    public const long StrangerId = 99_001;
    public const int PartyId = 5;
    public const int OtherPartyId = 6;

    public static GameAccount FollowerAccount { get; } = new(8, "follower", "tag");

    public static GameAccount SecondAccount { get; } = new(9, "second", "tag");

    /// <summary>The follower's account, subscribed: with a subscribed master, a group the game follows (D476).</summary>
    public static GameAccount SubscribedFollowerAccount { get; } = FollowerAccount with { SubscriptionEnd = DateTimeOffset.MaxValue };

    public static CharacterKey Master { get; } = TestPlay.Key;

    public static CharacterKey Follower { get; } = new(FollowerAccount.Id, FollowerId);

    public static CharacterKey Second { get; } = new(SecondAccount.Id, SecondId);

    public static Task<(TestConnection Connection, BotActor Bot)> MasterAsync(TestPlay play, int cell, long mapId = TestPlay.MapId)
    {
        return play.EnterAsync(cell, mapId, TestPlay.Account, MasterId, "Master");
    }

    public static Task<(TestConnection Connection, BotActor Bot)> FollowerAsync(TestPlay play, int cell, long mapId = TestPlay.MapId, string name = "Follower", int level = 200)
    {
        return play.EnterAsync(cell, mapId, FollowerAccount, FollowerId, name, level);
    }

    /// <summary>A master and its follower, both accounts subscribed and both characters of level 200, in the master's party it leads: a group the game follows (D476).</summary>
    public static async Task<(TestConnection Master, BotActor MasterBot, TestConnection Follower, BotActor FollowerBot)> FollowedByTheGameAsync(TestPlay play, int masterCell, int followerCell)
    {
        ArgumentNullException.ThrowIfNull(play);

        var (masterConnection, master) = await play.EnterAsync(masterCell, TestPlay.MapId, TestPlay.SubscribedAccount, MasterId, "Master");
        var (connection, follower) = await play.EnterAsync(followerCell, TestPlay.MapId, SubscribedFollowerAccount, FollowerId, "Follower");

        masterConnection.Server(Party(PartyId, MasterId, MasterId));
        masterConnection.Server(new PartyNewMemberEvent { PartyId = PartyId, Member = new Character { Id = FollowerId } });
        await master.ProcessedAsync();

        return (masterConnection, master, connection, follower);
    }

    public static Task<(TestConnection Connection, BotActor Bot)> SecondAsync(TestPlay play, int cell, long mapId = TestPlay.MapId, string name = "Second")
    {
        return play.EnterAsync(cell, mapId, SecondAccount, SecondId, name);
    }

    /// <summary>A party as the server gives it to a member.</summary>
    public static PartyJoinEvent Party(int partyId, long leaderId, params long[] members)
    {
        var joined = new PartyJoinEvent { PartyId = partyId, Type = PartyType.Classical, LeaderId = leaderId };

        joined.Members.AddRange(members.Select(member => new Character { Id = member, Basics = new CharacterBasics { Name = $"Member {member}" } }));

        return joined;
    }

    /// <summary>The server names a map, the client asks what is on it, and the server places a character on a cell.</summary>
    public static void Arrive(TestConnection connection, long characterId, long mapId, int cell)
    {
        connection.Server(new MapCurrentEvent { MapId = mapId });
        connection.Client(new MapInformationRequest { MapId = mapId });
        connection.Server(TestPlay.Information(cell, mapId, characterId));
    }

    /// <summary>Waits for a character's n-th request, a walk, then shows it walked and confirmed; gives the cell it ends on.</summary>
    public static async Task<int> WalkedAsync(TestPlay play, TestConnection connection, long characterId, int count)
    {
        var request = Assert.IsType<MapMovementRequest>(await SentAsync(play, connection, count));
        var start = request.KeyMovements[0] & 0xFFF;
        var end = request.KeyMovements[^1] & 0xFFF;

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(request.MapId).Cells.Span, start, end, out var path));

        connection.Server(new MapMovementEvent { ActorId = characterId, Cells = { path.Cells.ToArray() } });
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());

        return end;
    }

    /// <summary>Waits until a connection carried a number of D3B's requests, the clock moving meanwhile; gives the last.</summary>
    public static async Task<Google.Protobuf.IMessage> SentAsync(TestPlay play, TestConnection connection, int count)
    {
        await UntilAsync(play, () => connection.Sent.Count >= count, $"{count} requests sent");

        return connection.Sent[count - 1];
    }

    /// <summary>Waits until a condition holds, the clock moving meanwhile, at most five minutes of it.</summary>
    public static async Task UntilAsync(TestPlay play, Func<bool> condition, string what)
    {
        var deadline = play.Time.GetUtcNow() + TimeSpan.FromMinutes(5);

        while (!condition())
        {
            Assert.True(play.Time.GetUtcNow() < deadline, $"Not {what} in five minutes of the clock.");
            play.Time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Lets the bots' loops and behaviors run a while, the clock still.</summary>
    public static async Task SettleAsync(params BotActor[] bots)
    {
        foreach (var bot in bots)
            await bot.ProcessedAsync();

        await Task.Delay(50, TestContext.Current.CancellationToken);

        foreach (var bot in bots)
            await bot.ProcessedAsync();
    }

    /// <summary>What a text says in English, its values written as they are.</summary>
    public static string Say(GameText? text)
    {
        Assert.NotNull(text);

        return string.Format(CultureInfo.InvariantCulture, text.Format.In(CultureInfo.InvariantCulture), [.. text.Values.Select(value => (object?)(value.Text ?? value.Id.ToString(CultureInfo.InvariantCulture)))]);
    }
}
