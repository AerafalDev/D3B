namespace D3B.Bot.Harvesting;

/// <summary>How long a resource takes to grow back once harvested, as the host's bots saw it (D145).</summary>
/// <param name="Longest">The longest growing back seen whole, a bot on the map from the harvest's end to the element offering its skill again; none until one was.</param>
/// <param name="Seen">How many growings back were seen whole.</param>
/// <param name="AtMost">The least time after a harvest a later visit found the element grown back: it takes no longer, another player having maybe harvested it meanwhile; none until one was.</param>
/// <param name="AtLeast">The longest time after a bot's harvest a later visit found the element still growing, at most <see cref="Ceiling"/> times the shortest time known: it may take that long; none until one was (D583, D587).</param>
public readonly record struct Regrowth(TimeSpan? Longest, int Seen, TimeSpan? AtMost, TimeSpan? AtLeast = null)
{
    /// <summary>How many times the shortest time known of a resource its bound from below may reach (D587).</summary>
    /// <remarks>
    /// Measured on the owner's Forgelance miner of 28 November: other players harvesting the ore unseen between two visits drove the
    /// bound of nearly every ore skill to the ceiling of D583, twice the shortest, and the miner waited 53 % of its run. A quarter more
    /// still covers skill 25's 11.4 to 18.4 min only in part: the tour hurries to a map once its shortest time is past rather than wait.
    /// </remarks>
    public const double Ceiling = 1.25;

    /// <summary>Gets how long a tour reckons the resource takes: the longest seen whole, else the least bound from above, raised to the bound from below; else not known.</summary>
    /// <remarks>
    /// One resource does not always take the same: measured on the owner's miner of 26 November, ore grown back 11.4 min after
    /// one harvest and 18.4 min after another, watched whole. The least bound from above alone made the fastest the reckoning, and
    /// the miner came back 18 to 30 min after its harvests to ore still growing; the bound from below, which those visits teach,
    /// lengthens it.
    /// </remarks>
    public TimeSpan? Reckoned
    {
        get
        {
            // The ceiling holds over a shortest time learned after the bound, or a bound the database kept from before it shrank or
            // under the ceiling of D583.
            var least = AtLeast is { } bound && Shortest is { } shortest && bound > shortest * Ceiling ? shortest * Ceiling : AtLeast;

            return (Longest ?? AtMost) is { } reckoned ? (least is { } floor && floor > reckoned ? floor : reckoned) : least;
        }
    }

    /// <summary>Gets the shortest time known of the resource, the longest seen whole or the least bound from above, whichever is shorter; none while neither is known.</summary>
    public TimeSpan? Shortest => (Longest, AtMost) switch
    {
        ({ } longest, { } most) => longest < most ? longest : most,
        (var longest, var most) => longest ?? most,
    };
}
