using System.Text.Json;
using System.Text.Json.Serialization;

namespace D3B.Bot.Reports;

/// <summary>How a manifest is written and read: camel-cased, the area as its name.</summary>
internal static class ReportJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
