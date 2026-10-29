using D3B.Data.Banks;
using D3B.Data.Icons;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;

namespace D3B.Data.Tests;

internal static class TestTables
{
    private static readonly int[] _replies = [259, 5422, 63_535];
    private static readonly int[] _itemIcons = [34_009, 38_017];
    private static readonly int[] _jobIcons = [1];

    /// <summary>Wheat and ash wood, of known types, and a test item whose type the client lacks.</summary>
    public static ItemCatalog Items()
    {
        return new ItemCatalog(
            new Item[] { new(289, 1001, 34, 1, 1, 34_009), new(303, 1002, 38, 1, 1, 38_017), new(34_569, 1003, 264, 0, 0, 0) },
            new ItemType[] { new(34, 2001, 9, 2), new(38, 2002, 9, 2) });
    }

    /// <summary>The lumberjack cuts ash; using a zaap belongs to no profession.</summary>
    public static JobCatalog Jobs()
    {
        return new JobCatalog(
            new Job[] { new(Job.Base, 3001, Job.NoIcon), new(2, 3002, 1) },
            new Skill[] { new(6, 4001, 2, 303, 1, 1, false), new(184, 4002, Job.Base, Skill.NoItem, 0, 1, false) });
    }

    public static InteractiveCatalog Interactives()
    {
        return new InteractiveCatalog(new Interactive[] { new(1, 5001), new(16, 5002) });
    }

    /// <summary>Wheat's and ash wood's icons, every pixel 1 and 2; the lumberjack's, every pixel 3.</summary>
    public static IconCatalog Icons()
    {
        return new IconCatalog(
            new IconTable(_itemIcons, Enumerable.Repeat((byte)1, IconTable.IconLength).Concat(Enumerable.Repeat((byte)2, IconTable.IconLength)).ToArray()),
            new IconTable(_jobIcons, Enumerable.Repeat((byte)3, IconTable.IconLength).ToArray()));
    }

    /// <summary>One bank; banker 100 has two chest replies, banker 6394 one.</summary>
    public static BankCatalog Banks()
    {
        return new BankCatalog(
            new Bank[] { new(2756, 191_104_002) },
            new Banker[] { new(100, 6001, 0, 2), new(6394, 6002, 2, 1) },
            _replies);
    }
}
