using System.Collections.Immutable;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.World;

/// <summary>What is selected in the world: the one thing the viewport, the Outliner and the Details panel agree on (D397).</summary>
/// <remarks>
/// A click in any of them selects here, and each shows the selection as it changes: the viewport lights it, the Outliner
/// highlights its row and the Details panel lists its properties. It holds the choice and nothing else, so that the
/// three never ask one another.
/// </remarks>
internal sealed class WorldSelection
{
    /// <summary>Gets what is selected.</summary>
    public WorldSelectionKind Kind { get; private set; }

    /// <summary>Gets the character selected, for a character.</summary>
    public CharacterKey? Character { get; private set; }

    /// <summary>Gets the id of the route or zone selected.</summary>
    public string? AssetId { get; private set; }

    /// <summary>Gets the step of the route the Details show, from 0, or -1 for none: the one chosen last of those selected.</summary>
    public int Step { get; private set; } = -1;

    /// <summary>Gets every step of the route selected, in order: several once Control or Shift chose more than one (D395, D396).</summary>
    public ImmutableArray<int> Steps { get; private set; } = [];

    /// <summary>Raised when the selection changed.</summary>
    public event Action? Changed;

    /// <summary>Tells whether a character is the one selected.</summary>
    /// <param name="key">The character.</param>
    /// <returns>Whether it is selected.</returns>
    public bool IsCharacter(CharacterKey key)
    {
        return Kind is WorldSelectionKind.Character && Character == key;
    }

    /// <summary>Tells whether a route is the one selected.</summary>
    /// <param name="id">The route's id.</param>
    /// <returns>Whether it is selected.</returns>
    public bool IsRoute(string id)
    {
        return Kind is WorldSelectionKind.Route && AssetId == id;
    }

    /// <summary>Tells whether a step of a route is selected, with others or alone.</summary>
    /// <param name="id">The route's id.</param>
    /// <param name="step">The step, from 0.</param>
    /// <returns>Whether it is selected.</returns>
    public bool IsStep(string id, int step)
    {
        return IsRoute(id) && Steps.Contains(step);
    }

    /// <summary>Tells whether a zone is the one selected.</summary>
    /// <param name="id">The zone's id.</param>
    /// <returns>Whether it is selected.</returns>
    public bool IsZone(string id)
    {
        return Kind is WorldSelectionKind.Zone && AssetId == id;
    }

    /// <summary>Selects a character.</summary>
    /// <param name="key">The character.</param>
    public void SelectCharacter(CharacterKey key)
    {
        Set(WorldSelectionKind.Character, key, null, -1, []);
    }

    /// <summary>Selects a route, and one of its steps.</summary>
    /// <param name="id">The route's id.</param>
    /// <param name="step">The step, from 0, or -1 for the route alone.</param>
    public void SelectRoute(string id, int step = -1)
    {
        Set(WorldSelectionKind.Route, null, id, step, step >= 0 ? [step] : []);
    }

    /// <summary>Selects several steps of a route.</summary>
    /// <param name="id">The route's id.</param>
    /// <param name="steps">The steps, from 0, in any order.</param>
    /// <param name="step">The one the Details show, or -1 for the last of the steps.</param>
    public void SelectSteps(string id, IEnumerable<int> steps, int step = -1)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var chosen = steps.Where(candidate => candidate >= 0).Distinct().Order().ToImmutableArray();

        Set(WorldSelectionKind.Route, null, id, chosen.Contains(step) ? step : chosen.IsEmpty ? -1 : chosen[^1], chosen);
    }

    /// <summary>Selects a zone.</summary>
    /// <param name="id">The zone's id.</param>
    public void SelectZone(string id)
    {
        Set(WorldSelectionKind.Zone, null, id, -1, []);
    }

    /// <summary>Selects nothing.</summary>
    public void Clear()
    {
        Set(WorldSelectionKind.None, null, null, -1, []);
    }

    private void Set(WorldSelectionKind kind, CharacterKey? character, string? assetId, int step, ImmutableArray<int> steps)
    {
        if (Kind == kind && Character == character && AssetId == assetId && Step == step && Steps.AsSpan().SequenceEqual(steps.AsSpan()))
            return;

        Kind = kind;
        Character = character;
        AssetId = assetId;
        Step = step;
        Steps = steps;
        Changed?.Invoke();
    }
}
