namespace D3B.Studio.Behaviors;

/// <summary>Why a profile did not start on a bot.</summary>
public enum ProfileStartFailure
{
    /// <summary>The profile is none of the project's.</summary>
    ProfileMissing,

    /// <summary>The profile's behavior is none of the project's.</summary>
    BehaviorMissing,

    /// <summary>The behavior, or a value the profile changes, does not hold: the problems say why.</summary>
    Invalid,

    /// <summary>The activity was not started, such as for a bot that runs one already.</summary>
    NotStarted,
}
