using D3B.Bot.Behaviors;
using D3B.Bot.Journal;

namespace D3B.Storage.Profiles;

/// <summary>A character's profile that did not start on its client's connection, and why (D99, D112).</summary>
/// <remarks>
/// The host's log names the bot by its character, so its problems leave it out (D114): without this entry, the app would
/// show a bot that does nothing, and nowhere why.
/// </remarks>
/// <param name="At">When.</param>
/// <param name="Profile">The profile's name, or its id when the project holds it no more.</param>
/// <param name="Failure">Why it did not start.</param>
/// <param name="Problems">What is wrong with its behavior, for <see cref="ProfileStartFailure.Invalid"/>.</param>
/// <param name="Error">What was thrown, for <see cref="ProfileStartFailure.NotStarted"/>.</param>
public sealed record ProfileNotStartedEntry(DateTimeOffset At, string Profile, ProfileStartFailure Failure, IReadOnlyList<BehaviorProblem> Problems, string? Error) : JournalEntry(At, 0);
