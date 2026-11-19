using D3B.Data.Challenges;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the client's challenges, their texts and their completion and activation criteria.</summary>
/// <remarks>
/// The extraction fails when a challenge names a text a language lacks or has no completion criterion, and when the table
/// is empty, so that a client changing them is noticed. A challenge may exclude one the table lacks, as the client's own
/// do: kept as the client writes it, and counted (client.md).
/// </remarks>
internal static class ChallengesExtractor
{
    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [UnityDataTable.PathOf(client, "challenges")];
    }

    public static ChallengeCatalog Read(ClientInstall client, IReadOnlySet<int> held)
    {
        var records = UnityDataTable.ReadById(client, "challenges").OrderBy(record => record.Int32("id")).ToList();
        var known = records.Select(record => record.Int32("id")).ToHashSet();
        var challenges = new List<Challenge>(records.Count);
        var incompatibles = new List<int>();
        var completions = new List<string>(records.Count);
        var activations = new List<string>(records.Count);
        var unknown = 0;

        foreach (var record in records)
        {
            var id = record.Int32("id");
            var excluded = record.Int32Array("incompatibleChallenges");
            var challenge = new Challenge(
                id,
                record.Int32("nameId"),
                record.Int32("descriptionId"),
                record.Int32("categoryId"),
                checked((int)record.UInt32("iconId")),
                checked((int)record.UInt32("targetMonsterId")),
                incompatibles.Count,
                excluded.Length);

            foreach (var text in (int[])[challenge.NameId, challenge.DescriptionId])
            {
                if (!held.Contains(text))
                    throw new InvalidDataException($"Challenge {id} names text {text}, which a language lacks.");
            }

            unknown += excluded.Count(other => !known.Contains(other));

            var completion = record.String("completionCriterion");

            if (completion.Length is 0)
                throw new InvalidDataException($"Challenge {id} has no completion criterion.");

            challenges.Add(challenge);
            incompatibles.AddRange(excluded);
            completions.Add(completion);
            activations.Add(record.String("activationCriterion"));
        }

        if (challenges.Count is 0)
            throw new InvalidDataException("The client's table of challenges is empty.");

        Console.WriteLine($"Challenges: {challenges.Count:N0} in {challenges.Select(challenge => challenge.CategoryId).Distinct().Count():N0} categories, {completions.Distinct(StringComparer.Ordinal).Count():N0} completion criteria; exclusions of a challenge the client lacks, kept: {unknown:N0}");

        return new ChallengeCatalog(challenges.ToArray(), incompatibles.ToArray(), completions.ToArray(), activations.ToArray());
    }
}
