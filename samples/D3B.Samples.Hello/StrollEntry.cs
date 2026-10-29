using D3B.Bot.Movement;

namespace D3B.Samples.Hello;

/// <summary>What the strolls did for a bot.</summary>
/// <param name="Arrivals">How many of its walks arrived, over every stroll.</param>
/// <param name="LastCell">The cell its last walk went to.</param>
/// <param name="LastOutcome">How its last walk ended.</param>
internal sealed record StrollEntry(int Arrivals, int LastCell, WalkOutcome LastOutcome);
