using D3B.Bot.Activities;

namespace D3B.Studio.Behaviors;

/// <summary>A profile started on a bot, or why it was not.</summary>
/// <param name="Run">The behavior's run, when it started.</param>
/// <param name="Failure">Why it did not start, when it did not.</param>
/// <param name="Problems">What is wrong with the behavior, for <see cref="ProfileStartFailure.Invalid"/>.</param>
/// <param name="Error">What the start threw, for <see cref="ProfileStartFailure.NotStarted"/>.</param>
public sealed record ProfileStart(ActivityRun? Run, ProfileStartFailure? Failure, IReadOnlyList<BehaviorProblem> Problems, string? Error);
