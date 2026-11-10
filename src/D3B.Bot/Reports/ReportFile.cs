namespace D3B.Bot.Reports;

/// <summary>A file of a report as its manifest lists it.</summary>
/// <param name="Name">The name in the bundle, such as <c>message.md</c>.</param>
/// <param name="Size">Its size in bytes.</param>
/// <param name="Sha256">Its SHA-256, in lowercase hexadecimal.</param>
public sealed record ReportFile(string Name, long Size, string Sha256);
