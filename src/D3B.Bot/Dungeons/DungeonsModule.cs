using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Dungeons;

/// <summary>Dungeons: the activity that runs the dungeons a profile lists, in turn, from their door to their exit (D520 to D527).</summary>
/// <remarks>Registers <see cref="DungeonActivity"/> as <c>dungeon</c>. Requires the bots module.</remarks>
public sealed class DungeonsModule : IModule
{
    /// <summary>The activity's id in the host's catalogue, which profiles name.</summary>
    public const string ActivityId = "dungeon";

    /// <summary>Registers what the module brings, for a host that builds its services apart, such as a test's.</summary>
    /// <param name="services">The host's services, which hold the installed build's tables.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddActivity<DungeonActivity, DungeonSettings>(ActivityId, DungeonTexts.Text("Name"), Describe);
    }

    private static void Describe(ActivitySettingsBuilder<DungeonSettings> settings)
    {
        settings.Section(DungeonTexts.Text("SectionWhat"));
        settings.Dungeons(s => s.Dungeons, DungeonTexts.Text("Dungeons")).Help(DungeonTexts.Text("DungeonsHelp"));
        settings.Number(s => s.Runs, DungeonTexts.Text("Runs"), 0, 1_000).Unit(DungeonTexts.Text("RunsUnit")).Help(DungeonTexts.Text("RunsHelp"));

        settings.Section(DungeonTexts.Text("SectionBank"));
        settings.Flag(s => s.GoOnFull, DungeonTexts.Text("GoOnFull")).Help(DungeonTexts.Text("GoOnFullHelp"));

        settings.Section(DungeonTexts.Text("SectionCare"));
        settings.Number(s => s.LifeShare, DungeonTexts.Text("LifeShare"), 10, 100).Percent().Help(DungeonTexts.Text("LifeShareHelp"));
        settings.Flag(s => s.Mule, DungeonTexts.Text("Mule")).Help(DungeonTexts.Text("MuleHelp"));
        settings.Flag(s => s.UseZaaps, DungeonTexts.Text("UseZaaps")).Help(DungeonTexts.Text("UseZaapsHelp")).Advanced();
        settings.Flag(s => s.RestartAfterFailure, DungeonTexts.Text("RestartAfterFailure")).Help(DungeonTexts.Text("RestartAfterFailureHelp")).Advanced();

        settings.Rule(s => s.Dungeons.Count > 0, DungeonTexts.Text("RuleDungeons"));
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();
        Register(bot.Services);
    }
}
