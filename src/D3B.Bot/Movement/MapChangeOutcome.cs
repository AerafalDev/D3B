namespace D3B.Bot.Movement;

/// <summary>How a map change ended.</summary>
public enum MapChangeOutcome
{
    /// <summary>The character is on the step's destination, placed: the server named the map, then said what is on it.</summary>
    Changed,

    /// <summary>The character is on another map than the step's destination, placed: the server sent it elsewhere.</summary>
    Elsewhere,

    /// <summary>The character walks already, uses an element, is in a dialog or an exchange, or where it stands is not known yet: nothing was sent.</summary>
    Busy,

    /// <summary>The step does not leave where the character stands, no cell it leaves from can be reached, the server offers no door's skill on its element, or there is no haven bag of the character's to leave: nothing was sent.</summary>
    NoPath,

    /// <summary>The walk to the cell the map is left from did not arrive (<see cref="WalkOutcome"/>).</summary>
    NotWalked,

    /// <summary>The server refused the use of the door, or the teleport, and the character stays where it is.</summary>
    Refused,

    /// <summary>The server named no new map in time.</summary>
    NoAnswer,

    /// <summary>The server named the new map, but did not say what is on it in time: where the character stands is not known.</summary>
    NotPlaced,

    /// <summary>The walk to the cell the map is left from arrived, and the character stays: what the journey was to stay for came meanwhile, such as a resource grown back (D495). Nothing was asked of the server.</summary>
    Stayed,
}
