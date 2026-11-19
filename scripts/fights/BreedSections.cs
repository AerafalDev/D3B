using System.Globalization;

namespace D3B.Scripts.Fights;

/// <summary>Sums of the benches' measures by the character's breed, which each bench prints as a section per breed.</summary>
/// <remarks>
/// The totals hide a breed the AI plays badly behind those it plays well: a change of the AI is read breed by breed
/// (D463). A breed is the client's id, 0 when the replay could not tell it; it prints counts and sums, never a value
/// of one fight.
/// </remarks>
internal sealed class BreedSections
{
    private readonly SortedDictionary<int, Dictionary<string, double>> _breeds = [];

    public IEnumerable<int> Breeds => _breeds.Keys;

    public static string Name(int breed)
    {
        return breed is 0 ? "breed unknown" : $"breed {breed.ToString(CultureInfo.InvariantCulture)}";
    }

    public static string Ratio(double foreseen, double came)
    {
        return came < 1 ? "-" : $"x{(foreseen / came).ToString("F1", CultureInfo.InvariantCulture)}";
    }

    public void Add(int? breed, string key, double value = 1)
    {
        var id = breed ?? 0;

        if (!_breeds.TryGetValue(id, out var sums))
            _breeds[id] = sums = new Dictionary<string, double>(StringComparer.Ordinal);

        sums[key] = sums.GetValueOrDefault(key) + value;
    }

    public double Of(int breed, string key)
    {
        return _breeds.TryGetValue(breed, out var sums) ? sums.GetValueOrDefault(key) : 0;
    }

    public double Total(string key)
    {
        return _breeds.Values.Sum(sums => sums.GetValueOrDefault(key));
    }

    /// <summary>Gets the keys of a breed that start with a prefix, without it, such as the spells planned and played.</summary>
    public IEnumerable<string> Suffixes(int breed, string prefix)
    {
        return _breeds.TryGetValue(breed, out var sums)
            ? sums.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Select(key => key[prefix.Length..])
            : [];
    }
}
