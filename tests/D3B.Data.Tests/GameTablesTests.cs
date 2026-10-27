using D3B.Data.Banks;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Tests.Packs;

namespace D3B.Data.Tests;

public sealed class GameTablesTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void EachTableReadsBackFromAPackOnce()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer =>
        {
            writer.Add(ItemCatalog.EntryName, TestTables.Items());
            writer.Add(JobCatalog.EntryName, TestTables.Jobs());
            writer.Add(InteractiveCatalog.EntryName, TestTables.Interactives());
            writer.Add(BankCatalog.EntryName, TestTables.Banks());
        }));

        var tables = new GameTables(pack);

        Assert.Same(tables.Items, tables.Items);
        Assert.Same(tables.Jobs, tables.Jobs);
        Assert.Same(tables.Interactives, tables.Interactives);
        Assert.Same(tables.Banks, tables.Banks);
        Assert.Equal(TestTables.Items().Items.ToArray(), tables.Items.Items.ToArray());
        Assert.Equal(TestTables.Items().Types.ToArray(), tables.Items.Types.ToArray());
        Assert.Equal(TestTables.Jobs().Jobs.ToArray(), tables.Jobs.Jobs.ToArray());
        Assert.Equal(TestTables.Jobs().Skills.ToArray(), tables.Jobs.Skills.ToArray());
        Assert.Equal(TestTables.Interactives().Interactives.ToArray(), tables.Interactives.Interactives.ToArray());
        Assert.Equal(TestTables.Banks().Banks.ToArray(), tables.Banks.Banks.ToArray());
        Assert.Equal(TestTables.Banks().Bankers.ToArray(), tables.Banks.Bankers.ToArray());
        Assert.Equal(TestTables.Banks().Replies.ToArray(), tables.Banks.Replies.ToArray());
    }

    [Fact]
    public void AMissingTableIsReportedEachTimeItIsAskedFor()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1))));

        var tables = new GameTables(pack);

        Assert.Throws<KeyNotFoundException>(() => tables.Items);
        Assert.Throws<KeyNotFoundException>(() => tables.Items);
        Assert.Throws<KeyNotFoundException>(() => tables.Jobs);
        Assert.Throws<KeyNotFoundException>(() => tables.Interactives);
        Assert.Throws<KeyNotFoundException>(() => tables.Banks);
    }
}
