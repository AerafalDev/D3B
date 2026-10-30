using D3B.Bot.Characters;

namespace D3B.Bot.Fights;

/// <summary>Gives every character D3B's defaults, "Standard": a host without a database, such as a test's (D233).</summary>
internal sealed class DefaultStrategies : IStrategySource
{
    /// <inheritdoc/>
    public StrategySettings Played(CharacterKey character)
    {
        return new StrategySettings();
    }
}
