using D3B.Bot.Characters;
using D3B.Bot.Fights;

namespace D3B.Bot.Tests.Runtime;

/// <summary>The strategy every character of a test plays, which the test sets.</summary>
internal sealed class TestStrategies : IStrategySource
{
    public StrategySettings Settings { get; set; } = new();

    public StrategySettings Played(CharacterKey character)
    {
        return Settings;
    }
}
