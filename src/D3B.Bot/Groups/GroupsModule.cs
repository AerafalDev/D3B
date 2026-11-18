using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Groups;

/// <summary>Groups: a master and its followers, each follower playing the "Follow" activity, which names its master (D430, D436).</summary>
/// <remarks>Registers <see cref="FollowActivity"/> as <c>follow</c>, with no preset: a profile names its master. The masters' gates and the host's groups are the bots module's (<see cref="Bots.Groups"/>), so that any profile leads. Requires the bots module.</remarks>
public sealed class GroupsModule : IModule
{
    /// <summary>The activity's id in the host's catalogue, which profiles name.</summary>
    public const string ActivityId = "follow";

    /// <summary>Registers what the module brings, for a host that builds its services apart, such as a test's.</summary>
    /// <param name="services">The host's services.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddActivity<FollowActivity, FollowSettings>(ActivityId, GroupTexts.Text("Name"), settings =>
        {
            settings.Character(s => s.Master, GroupTexts.Text("Master")).Help(GroupTexts.Text("MasterHelp")).Required();
            settings.Rule(s => s.Master.Length > 0, GroupTexts.Text("RuleMaster"));
        });
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();
        Register(bot.Services);
    }
}
