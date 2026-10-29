using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Harvesting;

/// <summary>Harvesting and banking, the first milestone (D16, D127): the activity the app starts, with its settings and their preview.</summary>
/// <remarks>
/// Registers <see cref="HarvestActivity"/> as <c>harvest</c>, <see cref="HarvestSightings"/>, which every bot of the host
/// shares (D128), and <see cref="HarvestPreviews"/>, which the app's profile editor asks. Requires the bots module.
/// </remarks>
public sealed class HarvestingModule : IModule
{
    /// <summary>The activity's id in the host's catalogue, which profiles name.</summary>
    public const string ActivityId = "harvest";

    /// <summary>Registers what the module brings, for a host that builds its services apart, such as a test's.</summary>
    /// <param name="services">The host's services, which hold the installed build's world and tables.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<HarvestSightings>();
        services.AddSingleton<HarvestPreviews>();
        services.AddActivity<HarvestActivity, HarvestSettings>(ActivityId, HarvestTexts.Text("Name"), settings =>
        {
            settings.Section(HarvestTexts.Text("SectionWhat"));
            settings.Resources(s => s.Resources, HarvestTexts.Text("Resources"))
                .Ordered()
                .Help(HarvestTexts.Text("ResourcesHelp"))
                .VisibleWhen(s => !s.EverythingKnown);
            settings.Flag(s => s.EverythingKnown, HarvestTexts.Text("EverythingKnown"));

            settings.Section(HarvestTexts.Text("SectionWhere"));
            settings.Area(s => s.Area, HarvestTexts.Text("Area")).Help(HarvestTexts.Text("AreaHelp"));
            settings.SubAreas(s => s.SubAreas, HarvestTexts.Text("SubAreas"))
                .Within(s => s.Area)
                .SuggestedBy(s => s.Resources)
                .Help(HarvestTexts.Text("SubAreasHelp"));

            settings.Section(HarvestTexts.Text("SectionBank"));
            settings.Number(s => s.FullAt, HarvestTexts.Text("FullAt"), 50, 100).Percent();
            settings.Choice(s => s.Deposit, HarvestTexts.Text("Deposit"), deposit => HarvestTexts.Text(deposit is HarvestDeposit.Harvest ? "DepositHarvest" : "DepositAll"));

            settings.Rule(s => s.EverythingKnown || s.Resources.Count > 0, HarvestTexts.Text("RuleWhat"));
            settings.Rule(s => s.Area is not null || s.SubAreas.Count > 0, HarvestTexts.Text("RuleWhere"));
            settings.Preview((s, services) => services.GetRequiredService<HarvestPreviews>().Of(s));
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
