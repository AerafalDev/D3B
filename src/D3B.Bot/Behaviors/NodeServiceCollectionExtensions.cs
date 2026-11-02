using System.Globalization;
using System.Text.RegularExpressions;
using D3B.Bot.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Behaviors;

/// <summary>Registers the kinds of nodes a module declares, which behaviors use (D298).</summary>
public static partial class NodeServiceCollectionExtensions
{
    /// <summary>Registers a kind of node in the host's <see cref="NodeCatalog"/>.</summary>
    /// <typeparam name="TNode">
    /// Its code: a <see cref="ConditionNode{TValues}"/>, an <see cref="EventNode{TValues}"/>, an <see cref="ActionNode{TValues}"/>
    /// or an <see cref="InstantAction{TValues}"/>, made once per host with its constructor's services.
    /// </typeparam>
    /// <typeparam name="TValues">Its values, <see cref="NoValues"/> when it has none.</typeparam>
    /// <param name="services">The host's services.</param>
    /// <param name="id">The kind's id, unique in the host, such as <c>pods.atLeast</c>, which behaviors' files name: never changed.</param>
    /// <param name="sentence">Its sentence, its values' keys between braces, such as <c>Pods at least {Percent} %</c>.</param>
    /// <param name="describe">Declares its values, help and example.</param>
    /// <returns>The services.</returns>
    /// <exception cref="InvalidOperationException">A node with the id is already registered, or the default values do not hold.</exception>
    /// <exception cref="ArgumentException">The code is no node's, or the sentence names a value not declared.</exception>
    public static IServiceCollection AddNode<TNode, TValues>(this IServiceCollection services, string id, LocalizedText sentence, Action<NodeBuilder<TValues>>? describe = null)
        where TNode : class
        where TValues : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(sentence);

        var role = typeof(ConditionNode<TValues>).IsAssignableFrom(typeof(TNode)) ? NodeRole.Condition
            : typeof(EventNode<TValues>).IsAssignableFrom(typeof(TNode)) ? NodeRole.Event
            : typeof(ActionNode<TValues>).IsAssignableFrom(typeof(TNode)) ? NodeRole.Action
            : throw new ArgumentException($"{typeof(TNode).Name} is no condition, event nor action of {typeof(TValues).Name}.", nameof(TNode));
        var builder = new NodeBuilder<TValues>();

        describe?.Invoke(builder);

        var values = builder.Values.Build();
        var keys = values.All.Select(setting => setting.Key).ToHashSet(StringComparer.Ordinal);

        // The neutral resources' sentence, English, names the values as every language's does.
        foreach (Match placeholder in Placeholder().Matches(sentence.In(CultureInfo.InvariantCulture)))
        {
            if (!keys.Contains(placeholder.Groups[1].Value))
                throw new ArgumentException($"The sentence of {id} names {placeholder.Groups[1].Value}, which is not one of its values.", nameof(sentence));
        }

        var definition = new NodeDefinition(
            id,
            role,
            sentence,
            builder.HelpText,
            builder.ExampleText,
            values,
            typeof(InstantAction<TValues>).IsAssignableFrom(typeof(TNode)),
            provider => ActivatorUtilities.CreateInstance<TNode>(provider));

        // While the modules build, so that a module reusing an id stops the host from starting, as activities do (D93).
        if (services.Any(descriptor => descriptor.ImplementationInstance is NodeDefinition registered && registered.Id == id))
            throw new InvalidOperationException($"A node with the id {id} is already registered.");

        services.AddSingleton(definition);

        return services;
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
