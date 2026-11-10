using System.Text;
using System.Text.Json.Nodes;
using D3B.Bot.Reports;

namespace D3B.Studio.Assets;

/// <summary>The assets a character was playing, as the files of a report: its profile, and every route, zone and behavior of the project the profile reaches (D410).</summary>
/// <remarks>
/// What ships with D3B is left out: the maintainer has it. A profile names a zone or a sub-area by an id in its values, which
/// <see cref="Asset.Uses"/> does not say, so a value that is the id of an asset counts as a use.
/// </remarks>
public static class ReportAssets
{
    /// <summary>Gets the files of an asset and of everything it reaches.</summary>
    /// <param name="library">The project.</param>
    /// <param name="id">The id of the asset, a profile.</param>
    /// <returns>One part an asset, named after its id; none when the id is no asset of the project.</returns>
    public static IReadOnlyList<ReportPart> Parts(AssetLibrary library, string id)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(id);

        var parts = new List<ReportPart>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>([id]);

        while (queue.TryDequeue(out var next))
        {
            if (!seen.Add(next) || library.Find(next) is not { IsGiven: false } asset)
                continue;

            parts.Add(new ReportPart(NameOf(asset.Id, parts), Encoding.UTF8.GetBytes(AssetFiles.Write(asset))));

            foreach (var used in asset.Uses())
                queue.Enqueue(used);

            if (asset is ProfileAsset profile)
            {
                foreach (var value in profile.Values.Values.SelectMany(Strings))
                    queue.Enqueue(value);
            }
        }

        return parts;
    }

    // An id is a GUID in the project's files, but a hand-written one may hold a space or a slash.
    private static string NameOf(string id, List<ReportPart> taken)
    {
        var name = ReportFiles.AssetsFolder + id + ".json";

        return ReportFiles.IsValid(name) && taken.All(part => part.Name != name) ? name : $"{ReportFiles.AssetsFolder}asset-{taken.Count + 1}.json";
    }

    private static IEnumerable<string> Strings(JsonNode? node)
    {
        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => [text],
            JsonArray array => array.SelectMany(Strings),
            JsonObject members => members.SelectMany(member => Strings(member.Value)),
            _ => [],
        };
    }
}
