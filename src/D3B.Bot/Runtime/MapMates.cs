using D3B.Bot.Characters;

namespace D3B.Bot.Runtime;

/// <summary>The cells the host's bots stand on or walk to, map by map, so that no two of them walk to one cell at once (D456).</summary>
/// <remarks>
/// Measured on the owner's runs of 2026-11-19: of the walks of a character the server held no walk of, to a cell another
/// bot of its map was already walking to, the clients followed 9 of 28; to a free cell, 201 of 207. A bot claims the cell
/// it walks to before its request goes, the claim taken under one lock for the whole host, so that bots choosing at once
/// choose apart: their states show a walk only once the server answers it, 28 ms after.
/// </remarks>
internal sealed class MapMates
{
    private readonly Func<IReadOnlyCollection<BotActor>> _bots;
    private readonly Lock _lock = new();
    private readonly Dictionary<(long Map, int Cell), CharacterKey> _claims = [];
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes at the next claim or release.</summary>
    public Task Changed
    {
        get
        {
            lock (_lock)
                return _changed.Task;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="MapMates"/> class.</summary>
    /// <param name="bots">The host's bots, read each time.</param>
    public MapMates(Func<IReadOnlyCollection<BotActor>> bots)
    {
        _bots = bots;
    }

    /// <summary>Gets the cells of a map that another bot stands on, walks to or claims.</summary>
    /// <param name="bot">The bot asking.</param>
    /// <param name="mapId">Its map.</param>
    /// <returns>The cells.</returns>
    public IReadOnlySet<int> Taken(BotActor bot, long mapId)
    {
        var taken = new HashSet<int>();

        lock (_lock)
        {
            foreach (var ((map, cell), owner) in _claims)
            {
                if (map == mapId && owner != bot.Key)
                    taken.Add(cell);
            }
        }

        foreach (var other in Others(bot, mapId))
            taken.Add(other.IsWalking ? other.Path[^1] : other.Cell!.Value);

        return taken;
    }

    /// <summary>Claims a cell for a bot, unless another bot claims it.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="mapId">Its map.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="made">Whether this call made the claim, which the bot did not hold before.</param>
    /// <returns>Whether the bot holds the claim.</returns>
    public bool TryClaim(BotActor bot, long mapId, int cell, out bool made)
    {
        lock (_lock)
        {
            made = false;

            if (_claims.TryGetValue((mapId, cell), out var owner))
                return owner == bot.Key;

            _claims[(mapId, cell)] = bot.Key;
            made = true;
            Raise();

            return true;
        }
    }

    /// <summary>Releases a bot's claim on a cell.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="mapId">Its map.</param>
    /// <param name="cell">The cell.</param>
    public void Release(BotActor bot, long mapId, int cell)
    {
        lock (_lock)
        {
            if (_claims.TryGetValue((mapId, cell), out var owner) && owner == bot.Key && _claims.Remove((mapId, cell)))
                Raise();
        }
    }

    /// <summary>Gets another bot of the map that walks to a cell, if one does.</summary>
    /// <param name="bot">The bot asking.</param>
    /// <param name="mapId">Its map.</param>
    /// <param name="cell">The cell.</param>
    /// <returns>That bot, or <see langword="null"/>.</returns>
    public BotActor? WalkingTo(BotActor bot, long mapId, int cell)
    {
        return Others(bot, mapId).FirstOrDefault(other => other.IsWalking && other.Path[^1] == cell) is { } walking && Bot(walking) is { } walker ? walker : null;
    }

    // The states of the others placed on the map, out of a fight, whose clients play them: a fight's cells are the fight's.
    // Each state is read once, as another bot's loop replaces it at any time.
    private IEnumerable<CharacterState> Others(BotActor bot, long mapId)
    {
        return _bots().Where(other => other != bot && other.IsConnected)
            .Select(other => other.State)
            .Where(state => state is { Cell: not null, IsFighting: false } && state.MapId == mapId);
    }

    private BotActor? Bot(CharacterState state)
    {
        return _bots().FirstOrDefault(other => other.Key == state.Key);
    }

    private void Raise()
    {
        var changed = _changed;

        _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        changed.TrySetResult();
    }
}
