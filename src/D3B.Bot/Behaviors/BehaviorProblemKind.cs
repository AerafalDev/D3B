namespace D3B.Bot.Behaviors;

/// <summary>What is wrong with a behavior, or with a profile of it, so that it does not run (D297, D302).</summary>
public enum BehaviorProblemKind
{
    /// <summary>The profile's behavior is none of the project's.</summary>
    BehaviorMissing,

    /// <summary>Two states, rules or nodes share an id.</summary>
    IdRepeated,

    /// <summary>A node names a kind no module declares.</summary>
    NodeUnknown,

    /// <summary>A node is where its kind does not go, such as an event among actions or a condition on the way of execution.</summary>
    NodeMisplaced,

    /// <summary>A node names a value its kind does not have.</summary>
    ValueUnknown,

    /// <summary>A value does not read as its kind's, such as a word for a number.</summary>
    ValueDoesNotRead,

    /// <summary>A node's values do not hold, such as a number out of its bounds, as an activity's settings would not (D98).</summary>
    ValueInvalid,

    /// <summary>A value is bound to a public setting the behavior does not declare.</summary>
    ParameterUnknown,

    /// <summary>A value the profile changes does not read as its public setting's (D300).</summary>
    ProfileValueDoesNotRead,

    /// <summary>A value is bound to, or sets, a variable the behavior does not declare.</summary>
    VariableUnknown,

    /// <summary>A wire, or a variable, brings a value of another type than the input's (D311).</summary>
    TypesDiffer,

    /// <summary>A wire leaves or reaches a node or pin that is not there, or two leave one way out.</summary>
    LinkBroken,

    /// <summary>The way of execution comes back on itself, or values are wired in a circle.</summary>
    Loop,

    /// <summary>A rule plays an action that waits, which only a state may play.</summary>
    NotInstant,

    /// <summary>A state holds both states and actions.</summary>
    StateMixed,
}
