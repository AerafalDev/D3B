using System.Text.Json.Serialization;

namespace D3B.Bot.Reports;

/// <summary>How a manifest is written and read: camel-cased, the area as its name, generated so that a tool built without reflection reads it too.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ReportManifest))]
internal sealed partial class ReportJson : JsonSerializerContext
{
}
