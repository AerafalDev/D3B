namespace D3B.App.ViewModels.Characters;

/// <summary>What a row of a character's rail is: the layer Always, one of its rules, or a state (D325).</summary>
internal enum RailRowKind
{
    /// <summary>The layer Always, its heading.</summary>
    Layer,

    /// <summary>A rule of the layer Always: a threshold the activity watches, or a state of the layer.</summary>
    Rule,

    /// <summary>A state of the behavior.</summary>
    State,
}
