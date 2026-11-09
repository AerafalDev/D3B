using D3B.Bot.Localization;

namespace D3B.Studio.Behaviors;

/// <summary>A step of a state, played or being played, as the run's trace says it (D327).</summary>
/// <param name="Node">The node's sentence, or the activity's name.</param>
/// <param name="Doing">What the activity said it does once its step was played, if it says.</param>
/// <param name="Done">Whether the step played the action or the activity to its end.</param>
internal sealed record PlayedStep(LocalizedText Node, GameText? Doing, bool Done);
