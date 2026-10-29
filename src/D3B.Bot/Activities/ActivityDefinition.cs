using D3B.Bot.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Activities;

/// <summary>An activity the app can start on a bot: its id, its name, and how one is made (D87, D92).</summary>
/// <remarks>
/// A module registers it with <see cref="ActivityServiceCollectionExtensions.AddActivity{TActivity}(IServiceCollection, string, LocalizedText)"/>;
/// <see cref="ActivityCatalog"/> lists them.
/// </remarks>
public sealed class ActivityDefinition
{
    private readonly Func<IServiceProvider, BotActivity> _create;

    /// <summary>Gets the activity's id, unique in the host.</summary>
    public string Id { get; }

    /// <summary>Gets the activity's name, as the app shows it, in its language (D96).</summary>
    public LocalizedText Name { get; }

    /// <summary>Initializes a new instance of the <see cref="ActivityDefinition"/> class, its name the same in every language.</summary>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it.</param>
    /// <param name="create">Makes the activity, a new one for each start, from the host's services.</param>
    public ActivityDefinition(string id, string name, Func<IServiceProvider, BotActivity> create)
        : this(id, LocalizedText.Invariant(name), create)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ActivityDefinition"/> class.</summary>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it, in its language.</param>
    /// <param name="create">Makes the activity, a new one for each start, from the host's services.</param>
    public ActivityDefinition(string id, LocalizedText name, Func<IServiceProvider, BotActivity> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(create);

        Id = id;
        Name = name;
        _create = create;
    }

    /// <summary>Makes the activity, to start it on a bot.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>A new activity: one activity runs on one bot, once.</returns>
    public BotActivity Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return _create(services);
    }
}
