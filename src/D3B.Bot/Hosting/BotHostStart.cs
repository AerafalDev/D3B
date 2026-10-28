using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Hosting;

/// <summary>What starting the bots' host gave: the host, or why it did not start.</summary>
/// <param name="Host">The host, started, which the caller stops and disposes; <see langword="null"/> when it did not start.</param>
/// <param name="Failure">Why the host did not start, and what to do about it; <see langword="null"/> when it started.</param>
public sealed record BotHostStart(IHost? Host, string? Failure);
