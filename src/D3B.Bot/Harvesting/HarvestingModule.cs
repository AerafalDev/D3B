using D3B.Bot.Activities;
using D3B.Bot.Interactives;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using D3B.Data;
using D3B.Data.Packs;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // The bots module teaches it; a test's host without one gets it empty.
        services.TryAddSingleton<GraphicSkills>();
        services.TryAddSingleton(services => new GraphicPlaces(services.GetRequiredService<GamePack>(), services.GetRequiredService<GameWorld>()));
        services.AddSingleton<HarvestSightings>();

        // Whatever the bots do, every map they are on teaches how long its resources take to grow back (D145).
        services.TryAddSingleton<Regrowths>();
        services.AddSingleton<IGameObserver>(services => new RegrowthLearner(services.GetRequiredService<Regrowths>(), services.GetRequiredService<GameTables>(), services.GetRequiredService<TimeProvider>()));
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
            settings.Flag(s => s.IsolatedMaps, HarvestTexts.Text("IsolatedMaps")).Help(HarvestTexts.Text("IsolatedMapsHelp"));

            settings.Section(HarvestTexts.Text("SectionBank"));
            settings.Number(s => s.FullAt, HarvestTexts.Text("FullAt"), 50, 100).Percent();
            settings.Choice(s => s.Deposit, HarvestTexts.Text("Deposit"), deposit => HarvestTexts.Text(deposit is HarvestDeposit.Harvest ? "DepositHarvest" : "DepositAll"));

            settings.Choice(s => s.DepositTo, HarvestTexts.Text("DepositTo"), place => HarvestTexts.Text($"DepositTo{place}"))
                .Help(HarvestTexts.Text("DepositToHelp"));

            settings.Section(HarvestTexts.Text("SectionUpkeep"));
            settings.Flag(s => s.OpenBags, HarvestTexts.Text("OpenBags")).Help(HarvestTexts.Text("OpenBagsHelp"));
            settings.Flag(s => s.Heal, HarvestTexts.Text("Heal"));
            settings.Number(s => s.HealBelow, HarvestTexts.Text("HealBelow"), 10, 95)
                .Percent()
                .VisibleWhen(s => s.Heal);
            settings.Items(s => s.Food, HarvestTexts.Text("Food"))
                .Ordered()
                .OfItemTypes([.. HarvestActivity.FoodTypes])
                .Help(HarvestTexts.Text("FoodHelp"))
                .VisibleWhen(s => s.Heal);

            settings.Section(HarvestTexts.Text("SectionFight"));
            settings.Choice(s => s.AfterFight, HarvestTexts.Text("AfterFight"), after => HarvestTexts.Text($"AfterFight{after}"))
                .Help(HarvestTexts.Text("AfterFightHelp"));

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
