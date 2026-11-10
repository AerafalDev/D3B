namespace D3B.Bot.Reports;

/// <summary>A file of a report that is no capture and no message: a text, some JSON.</summary>
/// <param name="Name">The name in the bundle; see <see cref="ReportFiles.IsValid"/>.</param>
/// <param name="Data">Its bytes.</param>
public sealed record ReportPart(string Name, byte[] Data);
