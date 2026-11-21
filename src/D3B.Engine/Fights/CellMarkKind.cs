namespace D3B.Engine.Fights;

/// <summary>When a mark on cells casts its spell, as the effect that set it says.</summary>
public enum CellMarkKind
{
    /// <summary>A mark the engine does not foresee.</summary>
    Other,

    /// <summary>A glyph cast on a fighter there as its turn begins: effect 401.</summary>
    TurnStart,

    /// <summary>A glyph cast on a fighter there as its turn ends: effect 402.</summary>
    TurnEnd,

    /// <summary>A glyph cast on the fighters there as it comes, and on one that comes into it: effect 1091.</summary>
    Aura,

    /// <summary>A trap, cast as a fighter comes onto it: effect 400.</summary>
    Trap,

    /// <summary>An Eliotrope's portal, which casts nothing: a cast on it comes out of another (<see cref="Portals"/>), effect 1181.</summary>
    Portal,

    /// <summary>A Huppermage's rune, cast only when its author triggers it (effect 2023, <see cref="Glyphs"/>), then gone: effect 2022.</summary>
    Rune,
}
