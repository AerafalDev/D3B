namespace MyPlugin;

/// <summary>The activity's settings, which a profile keeps and the app edits: their initial values are the defaults.</summary>
/// <remarks>
/// The module declares each one as it registers the activity, with its name, its bounds and its help: a flag, a number,
/// a text, a choice of an enumeration's values, or the game's professions, areas, sub-areas and resources, one or a list.
/// </remarks>
public sealed class MyPluginSettings
{
    /// <summary>Gets or sets how many steps the activity takes.</summary>
    public int Steps { get; set; } = 1;
}
