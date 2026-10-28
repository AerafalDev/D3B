using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Activities;

/// <summary>Registers the activities a module brings, for the app to start (D92).</summary>
public static class ActivityServiceCollectionExtensions
{
    /// <summary>Registers an activity in the host's <see cref="ActivityCatalog"/>.</summary>
    /// <typeparam name="TActivity">The activity, made anew for each start with its constructor's services.</typeparam>
    /// <param name="services">The host's services.</param>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it.</param>
    /// <returns>The services.</returns>
    /// <exception cref="InvalidOperationException">An activity with the id is already registered.</exception>
    public static IServiceCollection AddActivity<TActivity>(this IServiceCollection services, string id, string name)
        where TActivity : BotActivity
    {
        ArgumentNullException.ThrowIfNull(services);

        var definition = new ActivityDefinition(id, name, provider => ActivatorUtilities.CreateInstance<TActivity>(provider));

        // While the modules build, so that a plugin reusing an id stops the host from starting, as an invalid plugin does (D21).
        if (services.Any(descriptor => descriptor.ImplementationInstance is ActivityDefinition registered && registered.Id == id))
            throw new InvalidOperationException($"An activity with the id {id} is already registered.");

        services.AddSingleton(definition);

        return services;
    }
}
