using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D3B.Bot.Transfers;

/// <summary>Transfers: the activity that moves objects between the character's bank and its guild's chest, either way (D560).</summary>
/// <remarks>Registers <see cref="TransferActivity"/> as <c>transfer</c>, with its settings, and the host's <see cref="TransferBags"/>. Requires the bots module.</remarks>
public sealed class TransfersModule : IModule
{
    /// <summary>The activity's id in the host's catalogue, which profiles name.</summary>
    public const string ActivityId = "transfer";

    /// <summary>The most of each item a profile may name to move or to keep.</summary>
    public const int MostAmount = 1_000_000;

    /// <summary>Registers what the module brings, for a host that builds its services apart, such as a test's.</summary>
    /// <param name="services">The host's services.</param>
    public static void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TransferBags>();
        services.AddActivity<TransferActivity, TransferSettings>(ActivityId, TransferTexts.Text("Name"), Describe);
    }

    /// <inheritdoc/>
    public void Build(BotBuilder bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        bot.AddModule<BotsModule>();
        Register(bot.Services);
    }

    private static void Describe(ActivitySettingsBuilder<TransferSettings> settings)
    {
        settings.Section(TransferTexts.Text("SectionWhat"));
        settings.Choice(s => s.Direction, TransferTexts.Text("Direction"), direction => TransferTexts.Text($"Direction{direction}")).Help(TransferTexts.Text("DirectionHelp"));
        settings.Flag(s => s.Everything, TransferTexts.Text("Everything")).Help(TransferTexts.Text("EverythingHelp"));
        settings.Choices(s => s.Categories, TransferTexts.Text("Categories"), category => TransferTexts.Text($"Category{category}"))
            .Help(TransferTexts.Text("CategoriesHelp"))
            .VisibleWhen(s => !s.Everything);
        settings.ItemTypes(s => s.Types, TransferTexts.Text("Types")).Help(TransferTexts.Text("TypesHelp")).VisibleWhen(s => !s.Everything);
        settings.Items(s => s.Items, TransferTexts.Text("Items")).Help(TransferTexts.Text("ItemsHelp")).VisibleWhen(s => !s.Everything);
        settings.ItemTypes(s => s.ExcludedTypes, TransferTexts.Text("ExcludedTypes")).Help(TransferTexts.Text("ExcludedTypesHelp")).Advanced();
        settings.Items(s => s.ExcludedItems, TransferTexts.Text("ExcludedItems")).Help(TransferTexts.Text("ExcludedItemsHelp")).Advanced();
        settings.Rule(s => s.Everything || s.Categories.Count + s.Types.Count + s.Items.Count > 0, TransferTexts.Text("RuleSomething"));

        settings.Section(TransferTexts.Text("SectionHowMany"));
        settings.Choice(s => s.Quantity, TransferTexts.Text("Quantity"), quantity => TransferTexts.Text($"Quantity{quantity}")).Help(TransferTexts.Text("QuantityHelp"));
        settings.Number(s => s.Amount, TransferTexts.Text("Amount"), 1, MostAmount)
            .Unit(TransferTexts.Text("AmountUnit"))
            .VisibleWhen(s => s.Quantity is not TransferQuantity.All);

        settings.Section(TransferTexts.Text("SectionOptions"));
        settings.Choice(s => s.WhenRefused, TransferTexts.Text("WhenRefused"), refusal => TransferTexts.Text($"WhenRefused{refusal}")).Help(TransferTexts.Text("WhenRefusedHelp"));
        settings.Flag(s => s.EmptyBagsFirst, TransferTexts.Text("EmptyBagsFirst")).Help(TransferTexts.Text("EmptyBagsFirstHelp")).VisibleWhen(s => s.Direction is TransferDirection.GuildChestToBank);
        settings.Flag(s => s.DryRun, TransferTexts.Text("DryRun")).Help(TransferTexts.Text("DryRunHelp"));
        settings.Flag(s => s.UseZaaps, TransferTexts.Text("UseZaaps")).Help(TransferTexts.Text("UseZaapsHelp")).Advanced();
    }
}
