using MemoryPack;

namespace D3B.Data.Challenges;

/// <summary>The client's challenges: their texts, their categories, what each excludes, and their completion and activation criteria (D464).</summary>
/// <remarks>
/// <para>
/// 842 challenges of client 3.7.14.18, which the server names a fight's by (D461). The table is sorted by identifier and found
/// by binary search; each challenge's criteria are kept beside it, in the same order, as the client writes them (client.md):
/// the AI reads a fight's completion criterion to know what the challenge asks of its turns.
/// </para>
/// <para>Loaded once and shared: nothing in it changes.</para>
/// </remarks>
[MemoryPackable]
public sealed partial class ChallengeCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "challenges/catalog";

    /// <summary>Gets the challenges, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Challenge> Challenges { get; }

    /// <summary>Gets the challenges each excludes, sliced by <see cref="Challenge.FirstIncompatible"/> and <see cref="Challenge.IncompatibleCount"/>.</summary>
    public ReadOnlyMemory<int> Incompatibles { get; }

    /// <summary>Gets each challenge's completion criterion, in the order of <see cref="Challenges"/>.</summary>
    public ReadOnlyMemory<string> Completions { get; }

    /// <summary>Gets each challenge's activation criterion, in the order of <see cref="Challenges"/>.</summary>
    public ReadOnlyMemory<string> Activations { get; }

    /// <summary>Initializes a new instance of the <see cref="ChallengeCatalog"/> class.</summary>
    /// <param name="challenges">The challenges, in strictly ascending order of identifier.</param>
    /// <param name="incompatibles">The challenges each excludes, in the order of their slices.</param>
    /// <param name="completions">The completion criteria, one a challenge.</param>
    /// <param name="activations">The activation criteria, one a challenge.</param>
    /// <exception cref="ArgumentException">The table is not in strictly ascending order, its slices do not follow one another, or a challenge lacks its criteria.</exception>
    [MemoryPackConstructor]
    public ChallengeCatalog(ReadOnlyMemory<Challenge> challenges, ReadOnlyMemory<int> incompatibles, ReadOnlyMemory<string> completions, ReadOnlyMemory<string> activations)
    {
        Tables.CheckAscending(challenges.Span, static challenge => challenge.Id, nameof(challenges));
        Tables.CheckSlices(challenges.Span, static challenge => (challenge.FirstIncompatible, challenge.IncompatibleCount), incompatibles.Length, nameof(incompatibles));

        if (completions.Length != challenges.Length)
            throw new ArgumentException($"{completions.Length} completion criteria for {challenges.Length} challenges.", nameof(completions));

        if (activations.Length != challenges.Length)
            throw new ArgumentException($"{activations.Length} activation criteria for {challenges.Length} challenges.", nameof(activations));

        Challenges = challenges;
        Incompatibles = incompatibles;
        Completions = completions;
        Activations = activations;
    }

    /// <summary>Finds a challenge.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <param name="challenge">The challenge, when found.</param>
    /// <returns>Whether the table holds it.</returns>
    public bool TryGetChallenge(int id, out Challenge challenge)
    {
        return Tables.TryFind(Challenges.Span, id, static challenge => challenge.Id, out challenge);
    }

    /// <summary>Gets a challenge's completion criterion, what winning it asks.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <returns>The criterion as the client writes it; null when the table lacks the challenge.</returns>
    public string? CompletionOf(int id)
    {
        var index = Tables.IndexOf(Challenges.Span, id, static challenge => challenge.Id);

        return index >= 0 ? Completions.Span[index] : null;
    }

    /// <summary>Gets a challenge's activation criterion, when the server may give it.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <returns>The criterion as the client writes it; null when the table lacks the challenge.</returns>
    public string? ActivationOf(int id)
    {
        var index = Tables.IndexOf(Challenges.Span, id, static challenge => challenge.Id);

        return index >= 0 ? Activations.Span[index] : null;
    }

    /// <summary>Gets the challenges one excludes from the same fight.</summary>
    /// <param name="id">The client's identifier.</param>
    /// <returns>Their identifiers; none when the table lacks the challenge.</returns>
    public ReadOnlySpan<int> IncompatibleWith(int id)
    {
        return TryGetChallenge(id, out var challenge) ? Incompatibles.Span.Slice(challenge.FirstIncompatible, challenge.IncompatibleCount) : [];
    }
}
