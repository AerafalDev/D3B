using System.Collections.Immutable;

namespace D3B.App.ViewModels.Routes;

/// <summary>The steps of a route that are selected: one, or several by Control and Shift (D395, D396).</summary>
/// <remarks>
/// It is a value: each gesture gives the selection it makes, and the editor keeps the one it holds. Whoever holds the steps
/// of the route says how many there are, so that a step that is not there is never selected.
/// </remarks>
/// <param name="Steps">The steps selected, from 0, in order, each once.</param>
/// <param name="Active">The step chosen last, which the Details show and the keys move from, or -1 when none is selected.</param>
/// <param name="From">The step a Shift click reaches out from: the one chosen alone or added last by Control, or -1.</param>
internal sealed record RouteStepSelection(ImmutableArray<int> Steps, int Active, int From)
{
    /// <summary>Gets the selection of no step.</summary>
    public static RouteStepSelection None { get; } = new([], -1, -1);

    /// <summary>Gets whether more than one step is selected.</summary>
    public bool IsSeveral => Steps.Length > 1;

    /// <summary>Selects one step alone.</summary>
    /// <param name="step">The step, from 0.</param>
    /// <param name="count">How many steps the route has.</param>
    /// <returns>The selection, none when the step is not in the route.</returns>
    public static RouteStepSelection Only(int step, int count)
    {
        return step >= 0 && step < count ? new RouteStepSelection([step], step, step) : None;
    }

    /// <summary>Selects the steps given that are in the route.</summary>
    /// <param name="steps">The steps, from 0, in any order.</param>
    /// <param name="active">The step chosen last, or -1 for the last of them.</param>
    /// <param name="count">How many steps the route has.</param>
    /// <returns>The selection.</returns>
    public static RouteStepSelection Of(IEnumerable<int> steps, int active, int count)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var chosen = steps.Where(step => step >= 0 && step < count).Distinct().Order().ToImmutableArray();

        if (chosen.IsEmpty)
            return None;

        var last = chosen.Contains(active) ? active : chosen[^1];

        return new RouteStepSelection(chosen, last, last);
    }

    /// <summary>Adds a step the selection does not hold, or takes off one it holds: a click with Control.</summary>
    /// <param name="step">The step, from 0.</param>
    /// <param name="count">How many steps the route has.</param>
    /// <returns>The selection after the click.</returns>
    public RouteStepSelection Toggled(int step, int count)
    {
        if (step < 0 || step >= count)
            return this;

        if (!Steps.Contains(step))
            return new RouteStepSelection(Steps.Add(step).Sort(), step, step);

        var rest = Steps.Remove(step);

        return rest.IsEmpty ? None : new RouteStepSelection(rest, rest[^1], rest[^1]);
    }

    /// <summary>Selects the steps from the one the selection reaches out from to a step: a click with Shift.</summary>
    /// <param name="step">The step, from 0.</param>
    /// <param name="count">How many steps the route has.</param>
    /// <returns>The selection after the click: the steps from <see cref="From"/> to the step, the step chosen last.</returns>
    public RouteStepSelection ExtendedTo(int step, int count)
    {
        if (step < 0 || step >= count)
            return this;

        var from = From >= 0 && From < count ? From : step;
        var low = Math.Min(from, step);
        var range = Enumerable.Range(low, Math.Max(from, step) - low + 1).ToImmutableArray();

        return new RouteStepSelection(range, step, from);
    }

    /// <summary>Keeps the steps that are in the route.</summary>
    /// <param name="count">How many steps the route has.</param>
    /// <returns>The selection, or that of the step chosen last alone when others have gone.</returns>
    public RouteStepSelection Pruned(int count)
    {
        return Steps.All(step => step < count) ? this : Only(Active, count);
    }
}
