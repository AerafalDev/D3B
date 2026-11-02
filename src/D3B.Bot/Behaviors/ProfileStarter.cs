using System.Globalization;
using D3B.Bot.Assets;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Behaviors;

/// <summary>Starts a profile on a bot: its behavior compiled with the values it changes, run as the bot's one activity (D297, D300).</summary>
/// <remarks>A behavior that does not hold, or a value the profile changes that no longer reads, stops the start and says why (D99).</remarks>
public sealed class ProfileStarter
{
    private readonly IServiceProvider _services;
    private readonly AssetLibrary _assets;
    private readonly NodeCatalog _nodes;
    private readonly CharacterVariables _characters;
    private readonly SharedVariables _shared;

    /// <summary>Initializes a new instance of the <see cref="ProfileStarter"/> class.</summary>
    /// <param name="services">The host's services, which make the activities, and the game's values the values name.</param>
    /// <param name="assets">The project.</param>
    /// <param name="nodes">The kinds of nodes.</param>
    /// <param name="characters">The variables the characters keep.</param>
    /// <param name="shared">The variables the bots share.</param>
    public ProfileStarter(IServiceProvider services, AssetLibrary assets, NodeCatalog nodes, CharacterVariables characters, SharedVariables shared)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(shared);

        _services = services;
        _assets = assets;
        _nodes = nodes;
        _characters = characters;
        _shared = shared;
    }

    /// <summary>Compiles a profile's behavior with its values, without starting it, to tell whether it runs.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The behavior ready to run, or what is wrong with it.</returns>
    public BehaviorCompilation Compile(ProfileAsset profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (_assets.Find<BehaviorAsset>(profile.Behavior) is not { } behavior)
            return new BehaviorCompilation(null, [new BehaviorProblem(BehaviorProblemKind.BehaviorMissing, profile.Id, $"The behavior {profile.Behavior} is none of the project's.")]);

        return BehaviorCompiler.Compile(behavior, profile.Values, _nodes, _services.GetService<GameReferences>());
    }

    /// <summary>Starts a profile on a bot.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="profileId">The profile's id.</param>
    /// <returns>The behavior's run, or why it did not start.</returns>
    public ProfileStart Start(BotActor bot, string profileId)
    {
        ArgumentNullException.ThrowIfNull(profileId);

        return _assets.Find<ProfileAsset>(profileId) is { } profile
            ? Start(bot, profile)
            : new ProfileStart(null, ProfileStartFailure.ProfileMissing, [], null);
    }

    /// <summary>Starts a profile on a bot.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="profile">The profile.</param>
    /// <returns>The behavior's run, or why it did not start.</returns>
    public ProfileStart Start(BotActor bot, ProfileAsset profile)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(profile);

        var compilation = Compile(profile);

        if (compilation.Behavior is not { } behavior)
        {
            var failure = compilation.Problems.Any(problem => problem.Kind is BehaviorProblemKind.BehaviorMissing) ? ProfileStartFailure.BehaviorMissing : ProfileStartFailure.Invalid;

            return new ProfileStart(null, failure, compilation.Problems, null);
        }

        var name = profile.Title?.In(CultureInfo.CurrentUICulture) ?? profile.Name;

        try
        {
            return new ProfileStart(bot.StartActivity(new BehaviorActivity(behavior, name, _services, _characters, _shared), name), null, [], null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The bot may already run an activity, or be stopped with the host.
            return new ProfileStart(null, ProfileStartFailure.NotStarted, [], exception.Message);
        }
    }
}
