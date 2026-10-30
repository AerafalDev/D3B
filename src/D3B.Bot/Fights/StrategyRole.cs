namespace D3B.Bot.Fights;

/// <summary>What a character's strategy has it do for its side (D234).</summary>
public enum StrategyRole
{
    /// <summary>Deals damage, as the AI did before strategies.</summary>
    Attacker,

    /// <summary>Holds its allies' life twice as dear, so that it heals and shields them first, and keeps back.</summary>
    Healer,

    /// <summary>Holds its allies' life dearer and its own less, closer to the enemies.</summary>
    Protector,
}
