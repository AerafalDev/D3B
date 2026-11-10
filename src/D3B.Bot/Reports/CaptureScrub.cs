namespace D3B.Bot.Reports;

/// <summary>What <see cref="CaptureScrubber.Scrub"/> found and overwrote in a capture.</summary>
/// <param name="Names">The names of the account's characters it hid.</param>
/// <param name="Secrets">The distinct secret values (the ticket and the client's opaque strings) it hid.</param>
/// <param name="Fields">The places overwritten, a place being a field or a run of bytes: 0 for a capture already clean.</param>
public readonly record struct CaptureScrub(int Names, int Secrets, int Fields);
