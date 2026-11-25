using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Settings;
using D3B.Bot.Transfers;
using D3B.Data.Items;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Transfers;

public sealed class TransfersModuleTests
{
    private static ActivityDefinition Transfer()
    {
        var services = new ServiceCollection();

        TransfersModule.Register(services);

        return Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());
    }

    [Fact]
    public void TheTransferIsNamedInFrenchAndAsksWhichWayWhatAndHowMany()
    {
        var transfer = Transfer();

        Assert.Equal(TransfersModule.ActivityId, transfer.Id);
        Assert.Equal("Transfert banque ↔ coffre de guilde", transfer.Name.In(CultureInfo.GetCultureInfo("fr")));
        Assert.Equal(
            [
                nameof(TransferSettings.Direction), nameof(TransferSettings.Everything), nameof(TransferSettings.Categories), nameof(TransferSettings.Types), nameof(TransferSettings.Items),
                nameof(TransferSettings.ExcludedTypes), nameof(TransferSettings.ExcludedItems), nameof(TransferSettings.Quantity), nameof(TransferSettings.Amount),
                nameof(TransferSettings.WhenRefused), nameof(TransferSettings.EmptyBagsFirst), nameof(TransferSettings.DryRun), nameof(TransferSettings.UseZaaps),
            ],
            transfer.Settings.All.Select(setting => setting.Key));
        Assert.Equal(SettingKind.ItemType, transfer.Settings.All.Single(setting => setting.Key == nameof(TransferSettings.Types)).Kind);
    }

    [Fact]
    public void AProfileThatChoosesNothingToMoveIsRefusedAndOneThatChoosesACategoryOrEverythingHolds()
    {
        var settings = Transfer().Settings;
        var game = TestReferences.Game();

        Assert.Contains(settings.Check(new TransferSettings(), game), problem => problem.Kind is SettingProblemKind.Rule);
        Assert.Empty(settings.Check(new TransferSettings { Categories = [ItemCategory.Resource] }, game));
        Assert.Empty(settings.Check(new TransferSettings { Everything = true }, game));
        Assert.Contains(settings.Check(new TransferSettings { Types = [9_999] }, game), problem => problem.Kind is SettingProblemKind.Unknown);
    }

    [Fact]
    public void TheHostKeepsOneTransferBagsForEveryRunOfTheTransfer()
    {
        var services = new ServiceCollection();

        TransfersModule.Register(services);

        using var provider = services.BuildServiceProvider();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TransferBags));
        Assert.Same(provider.GetRequiredService<TransferBags>(), provider.GetRequiredService<TransferBags>());
    }

    [Fact]
    public void AProfileIsReadBackWithItsChoices()
    {
        var settings = Transfer().Settings;
        var written = settings.Write(new TransferSettings { Direction = TransferDirection.GuildChestToBank, Categories = [ItemCategory.Equipment, ItemCategory.Cosmetic], Types = [38], Quantity = TransferQuantity.KeepInSource, Amount = 20 });
        var read = Assert.IsType<TransferSettings>(settings.Read(written).Settings);

        Assert.Equal(TransferDirection.GuildChestToBank, read.Direction);
        Assert.Equal([ItemCategory.Equipment, ItemCategory.Cosmetic], read.Categories);
        Assert.Equal([38], read.Types);
        Assert.Equal((TransferQuantity.KeepInSource, 20), (read.Quantity, read.Amount));
    }
}
