using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Zaaps;

/// <summary>Zaaps: the activity that walks to every zaap within reach the character does not know, so that it learns them (D499).</summary>
/// <remarks>Registers <see cref="ZaapDiscoveryActivity"/> as <c>zaaps</c>, with no setting. Requires the bots module.</remarks>
public sealed class ZaapsModule : IModule
{
    /// <summary>The activity's id in the host's catalogue, which profiles name.</summary>
    public const string ActivityId = "zaaps";

    /// <summary>Registers what the module brings, for a host that builds its services apart, such as a test's.</summary>
    /// <param name="services">The host's services.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddActivity<ZaapDiscoveryActivity>(ActivityId, ZaapTexts.Text("Name"));
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();
        Register(bot.Services);
    }
}
