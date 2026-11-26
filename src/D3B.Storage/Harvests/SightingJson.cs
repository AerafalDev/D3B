using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace D3B.Storage.Harvests;

/// <summary>Writes a map's elements as JSON in their column, and compares them by what they hold (D584).</summary>
internal static class SightingJson
{
    /// <summary>Gets the comparer of a map's elements.</summary>
    public static ValueComparer<List<KeptElement>> ElementsComparer { get; } = new(
        (left, right) => Write(left) == Write(right),
        elements => Write(elements).GetHashCode(StringComparison.Ordinal),
        elements => Read(Write(elements)));

    public static string Write(List<KeptElement>? elements)
    {
        return JsonSerializer.Serialize(elements ?? []);
    }

    public static List<KeptElement> Read(string json)
    {
        return JsonSerializer.Deserialize<List<KeptElement>>(json) ?? [];
    }
}
