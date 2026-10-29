namespace D3B.Bot.Activities;

/// <summary>Activities that come and go while the host runs, such as the scripts of a folder, listed in the <see cref="ActivityCatalog"/> after the modules' (D108).</summary>
/// <remarks>Read from any thread, such as the app's: its list is a snapshot, replaced whole, and its version tells a reader it changed.</remarks>
public interface IActivitySource
{
    /// <summary>Gets a number that changes whenever the activities do.</summary>
    int Version { get; }

    /// <summary>Gets the activities now.</summary>
    IReadOnlyList<ActivityDefinition> Activities { get; }
}
