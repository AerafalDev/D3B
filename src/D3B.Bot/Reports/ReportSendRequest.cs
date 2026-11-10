namespace D3B.Bot.Reports;

/// <summary>A report to send: the file written, what it says of itself, and the player's message, which goes in the post as text (D411).</summary>
/// <param name="FilePath">The <c>.d3breport</c> file.</param>
/// <param name="Manifest">Its manifest.</param>
/// <param name="Message">The player's own message, or null for none.</param>
public sealed record ReportSendRequest(string FilePath, ReportManifest Manifest, string? Message);
