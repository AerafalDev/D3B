namespace D3B.App.ViewModels.Characters;

/// <summary>A row of a character's rail: the layer Always, a rule with its gauge against its threshold, or a state, the one playing lit (D325).</summary>
/// <param name="Kind">What the row is.</param>
/// <param name="Text">What it says, such as "Pods ≥ 90 % → Banque".</param>
/// <param name="Depth">How deep a state sits under the others.</param>
internal sealed record RailRowViewModel(RailRowKind Kind, string Text, int Depth = 0)
{
    /// <summary>Gets whether the row is the state playing.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets what the row says beside it once active, such as "Actif".</summary>
    public string ActiveText { get; init; } = "";

    /// <summary>Gets the gauge's value, a share from 0 to 100, when the rule watches one.</summary>
    public double? Gauge { get; init; }

    /// <summary>Gets the threshold, a share from 0 to 100, which the gauge's mark shows.</summary>
    public double Threshold { get; init; }

    /// <summary>Gets whether the gauge is the life's, red.</summary>
    public bool IsLife { get; init; }

    /// <summary>Gets the gauge's tooltip: the value and the threshold.</summary>
    public string GaugeTip { get; init; } = "";

    /// <summary>Gets whether the row is the layer's heading.</summary>
    public bool IsLayer => Kind is RailRowKind.Layer;

    /// <summary>Gets whether the row is a rule.</summary>
    public bool IsRule => Kind is RailRowKind.Rule;

    /// <summary>Gets whether the row is a state.</summary>
    public bool IsState => Kind is RailRowKind.State;

    /// <summary>Gets the gauge's value, 0 when it has none.</summary>
    public double GaugeValue => Gauge ?? 0;

    /// <summary>Gets whether the row draws the pods' gauge.</summary>
    public bool HasPodsGauge => Gauge is not null && !IsLife;

    /// <summary>Gets whether the row draws the life's gauge, red.</summary>
    public bool HasLifeGauge => Gauge is not null && IsLife;

    /// <summary>Gets the indent of a state under another, in pixels.</summary>
    public double Indent => Depth * 14;
}
