using System.Collections.Frozen;
using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Bot.Groups;

/// <summary>The host's groups: which bot follows which master, while its "Follow" activity runs (D430, D437).</summary>
/// <remarks>
/// <para>
/// A follower joins its master's group as its activity starts and leaves it once the activity is over: a master leads as
/// long as a bot follows it, whatever profile it plays, and plays as before once none does. A bot of the group whose
/// client closed stays in it, so that the group waits for it (D432).
/// </para>
/// <para>
/// No follower ever leads (D432): a character that follows cannot be followed, nor one followed follow another, so that
/// a group is one master and its followers, at most as many as a party holds beside its leader.
/// </para>
/// </remarks>
public sealed class GroupDirectory
{
    /// <summary>The most followers a master leads: a party of the game holds eight characters, the master among them.</summary>
    public const int MostFollowers = 7;

    private readonly Lock _lock = new();
    private ImmutableList<Following> _following = [];
    private ImmutableDictionary<CharacterKey, GroupCollect> _collecting = ImmutableDictionary<CharacterKey, GroupCollect>.Empty;
    private int _rounds;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes at the next change of any group, a follower joining or leaving.</summary>
    public Task Changed
    {
        get
        {
            lock (_lock)
                return _changed.Task;
        }
    }

    /// <summary>Has a bot follow a master, until the membership given back is disposed.</summary>
    /// <param name="follower">The follower's bot.</param>
    /// <param name="master">The master's character.</param>
    /// <param name="refusal">Why the bot cannot follow it, when it cannot.</param>
    /// <returns>The membership, which the follower disposes once it no longer follows; <see langword="null"/> when refused.</returns>
    public IDisposable? Follow(BotActor follower, CharacterKey master, out FollowRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(follower);

        refusal = default;

        lock (_lock)
        {
            FollowRefusal? refused = follower.Key == master ? FollowRefusal.Itself
                : _following.Any(following => following.Follower.Key == follower.Key) ? FollowRefusal.Follows
                : _following.Any(following => following.Follower.Key == master) ? FollowRefusal.MasterFollows
                : _following.Any(following => following.Master == follower.Key) ? FollowRefusal.Leads
                : _following.Count(following => following.Master == master) >= MostFollowers ? FollowRefusal.Full
                : null;

            if (refused is { } reason)
            {
                refusal = reason;

                return null;
            }

            var membership = new Following(this, follower, master);

            _following = _following.Add(membership);
            Raise();

            return membership;
        }
    }

    /// <summary>Has a master take a follower's objects at the bank, until the round given back is disposed (D433, D442).</summary>
    /// <param name="master">The master's character.</param>
    /// <param name="follower">The follower whose turn it is.</param>
    /// <param name="food">Whether the food that only heals is given too (D459).</param>
    /// <param name="left">The follower's stacks an exchange of that unloading left, given no more (D459).</param>
    /// <param name="kept">What every member keeps, as the master's profile says, such as a dungeon's key (D529).</param>
    /// <returns>The round, which the master disposes once the exchange is over.</returns>
    public IDisposable Collect(CharacterKey master, CharacterKey follower, bool food, IEnumerable<int> left, Func<InventoryItem, bool>? kept = null)
    {
        ArgumentNullException.ThrowIfNull(left);

        lock (_lock)
        {
            var collect = new GroupCollect(follower, ++_rounds, food, left.ToFrozenSet()) { Kept = kept };

            _collecting = _collecting.SetItem(master, collect);
            Raise();

            return new Collecting(this, master, collect);
        }
    }

    /// <summary>Gets whose objects a master takes at the bank now, if it takes any.</summary>
    /// <param name="master">The master's character.</param>
    /// <returns>The follower and the round, or <see langword="null"/>.</returns>
    public GroupCollect? CollectingFrom(CharacterKey master)
    {
        return Volatile.Read(ref _collecting).GetValueOrDefault(master);
    }

    /// <summary>Gets the master a character follows, if it follows one.</summary>
    /// <param name="follower">The character.</param>
    /// <returns>Its master, or <see langword="null"/>.</returns>
    public CharacterKey? MasterOf(CharacterKey follower)
    {
        return Volatile.Read(ref _following).FirstOrDefault(following => following.Follower.Key == follower)?.Master;
    }

    /// <summary>Gets the bots following a master, in the order they joined it: the chain's order is <see cref="GroupChain.Order"/>'s.</summary>
    /// <param name="master">The master's character.</param>
    /// <returns>The followers' bots; none for a character nobody follows.</returns>
    public IReadOnlyList<BotActor> FollowersOf(CharacterKey master)
    {
        return [.. Volatile.Read(ref _following).Where(following => following.Master == master).Select(following => following.Follower)];
    }

    /// <summary>Completes <see cref="Changed"/> for a change of a group's part no bot's state shows, such as a follower placed whose readiness is the game's (D477).</summary>
    internal void Touch()
    {
        lock (_lock)
            Raise();
    }

    private void Leave(Following membership)
    {
        lock (_lock)
        {
            var left = _following.Remove(membership);

            if (left == _following)
                return;

            _following = left;
            Raise();
        }
    }

    private void EndCollect(CharacterKey master, GroupCollect collect)
    {
        lock (_lock)
        {
            if (_collecting.GetValueOrDefault(master) != collect)
                return;

            _collecting = _collecting.Remove(master);
            Raise();
        }
    }

    private void Raise()
    {
        var changed = _changed;

        _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        changed.TrySetResult();
    }

    private sealed class Collecting : IDisposable
    {
        private readonly GroupDirectory _groups;
        private readonly CharacterKey _master;
        private readonly GroupCollect _collect;

        public Collecting(GroupDirectory groups, CharacterKey master, GroupCollect collect)
        {
            _groups = groups;
            _master = master;
            _collect = collect;
        }

        public void Dispose()
        {
            _groups.EndCollect(_master, _collect);
        }
    }

    private sealed class Following : IDisposable
    {
        private readonly GroupDirectory _groups;

        public BotActor Follower { get; }

        public CharacterKey Master { get; }

        public Following(GroupDirectory groups, BotActor follower, CharacterKey master)
        {
            _groups = groups;
            Follower = follower;
            Master = master;
        }

        public void Dispose()
        {
            _groups.Leave(this);
        }
    }
}
