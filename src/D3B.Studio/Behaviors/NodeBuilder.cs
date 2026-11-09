using D3B.Bot.Localization;
using D3B.Bot.Settings;

namespace D3B.Studio.Behaviors;

/// <summary>Describes a kind of node as a module declares it: its values, help and example (D298).</summary>
/// <typeparam name="TValues">Its values: a class with a constructor of no parameter, whose initial values are the defaults.</typeparam>
public sealed class NodeBuilder<TValues>
    where TValues : class, new()
{
    /// <summary>Gets the description of its values, as an activity's settings (D98): each a property, of a kind, with its name and bounds.</summary>
    public ActivitySettingsBuilder<TValues> Values { get; } = new();

    internal LocalizedText? HelpText { get; private set; }

    internal LocalizedText? ExampleText { get; private set; }

    internal NodeBuilder()
    {
    }

    /// <summary>Gives the node its short help.</summary>
    /// <param name="help">The help, as the app shows it.</param>
    /// <returns>This builder.</returns>
    public NodeBuilder<TValues> Help(LocalizedText help)
    {
        ArgumentNullException.ThrowIfNull(help);

        HelpText = help;

        return this;
    }

    /// <summary>Gives the node an example of its use.</summary>
    /// <param name="example">The example, as the app shows it.</param>
    /// <returns>This builder.</returns>
    public NodeBuilder<TValues> Example(LocalizedText example)
    {
        ArgumentNullException.ThrowIfNull(example);

        ExampleText = example;

        return this;
    }
}
