namespace D3B.Engine.Tactics;

/// <summary>Where a challenge asks the fighter playing to end its turn (D466).</summary>
public enum TurnEnd
{
    /// <summary>Anywhere.</summary>
    Free,

    /// <summary>On a cell beside an ally's: Collant.</summary>
    BesideAnAlly,

    /// <summary>On no cell beside an ally's: Misanthrope.</summary>
    AwayFromAllies,

    /// <summary>On no cell beside an enemy's: Prudent.</summary>
    AwayFromEnemies,

    /// <summary>On a cell beside an enemy's: Hardi (D497).</summary>
    BesideAnEnemy,

    /// <summary>On a cell beside an obstacle, a hole or the map's edge: Raser les murs.</summary>
    BesideAnObstacle,

    /// <summary>In line with an ally, on its row or its column: De la même lignée.</summary>
    InLineWithAnAlly,

    /// <summary>On a diagonal of an ally's: La diagonale du plein.</summary>
    InDiagonalWithAnAlly,

    /// <summary>In the line of sight of an enemy: En ligne de mire.</summary>
    InSightOfAnEnemy,

    /// <summary>In the line of sight of no visible enemy: Pas vu pas pris.</summary>
    OutOfEnemiesSight,
}
