namespace D3B.Extractor.Unity;

/// <summary>A game data table: the one <c>DataRoot</c> of <c>Content/Data/data_assets_&lt;name&gt;dataroot.asset.bundle</c>.</summary>
/// <remarks>
/// Its <c>objectsById</c> pairs each key with a managed reference, and its registry holds the records, each read as
/// <see cref="Fields"/> through its class's type tree (<c>main:docs/client.md</c>, "Data tables").
/// </remarks>
internal static class DataTable
{
    /// <summary>The bundle of a table, such as <c>waypoints</c>.</summary>
    public static string PathOf(ClientInstall client, string name)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Data", $"data_assets_{name}dataroot.asset.bundle");
    }

    /// <summary>Reads every record of a table, in the order of its keys.</summary>
    /// <exception cref="InvalidDataException">The bundle holds no table, or a key names a record the table lacks.</exception>
    public static IReadOnlyList<(long Key, Fields Record)> Read(ClientInstall client, string name)
    {
        var path = PathOf(client, name);

        using var bundle = Bundle.Open(path);

        foreach (var table in bundle.MonoBehaviours())
        {
            if (table.Tree.Child(0, "objectsById") is not -1 && table.Tree.Child(0, "references") is not -1)
            {
                return Read(table);
            }
        }

        throw new InvalidDataException($"{path} holds no data table.");
    }

    /// <summary>Reads every record of a table whose records hold their own key in an <c>id</c>, in the order of its keys.</summary>
    /// <exception cref="InvalidDataException">The bundle holds no table, or a key names a record the table lacks or of another id.</exception>
    public static IReadOnlyList<Fields> ReadById(ClientInstall client, string name)
    {
        return ById(name, Read(client, name));
    }

    /// <summary>Reads the records of a table's object.</summary>
    /// <exception cref="InvalidDataException">The object is not laid out as a table, or a key names a record it lacks.</exception>
    public static IReadOnlyList<(long Key, Fields Record)> Read(SerializedObject table)
    {
        var tree = table.Tree;
        var reader = new ObjectReader(table.Data, tree);
        long[] keys = [];
        long[] values = [];
        var records = new Dictionary<long, Fields>();

        foreach (var child in tree[0].Children)
        {
            switch (tree[child].Name)
            {
                case "objectsById":
                    foreach (var part in tree[child].Children)
                    {
                        switch (tree[part].Name)
                        {
                            case "m_keys":
                                keys = reader.ReadInt64Array(part);
                                break;
                            case "m_values":
                                values = reader.ReadReferenceArray(part);
                                break;
                            default:
                                reader.Skip(part);
                                break;
                        }
                    }

                    reader.AlignAfter(child);
                    break;
                case "references":
                    ReferenceRegistry.Read(ref reader, table.Data, child, table.ReferenceTypes, (long id, string className, ref ObjectReader record) =>
                    {
                        if (!records.TryAdd(id, Fields.Read(ref record, 0, className)))
                        {
                            throw new InvalidDataException($"{tree.Name}: its registry holds record {id} twice.");
                        }

                        return true;
                    });
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.ExpectEnd();

        if (keys.Length != values.Length)
        {
            throw new InvalidDataException($"{tree.Name}: {keys.Length} keys for {values.Length} records.");
        }

        var rows = new List<(long Key, Fields Record)>(keys.Length);

        for (var i = 0; i < keys.Length; i++)
        {
            if (!records.TryGetValue(values[i], out var record))
            {
                throw new InvalidDataException($"{tree.Name}: key {keys[i]} names a record the table does not hold.");
            }

            rows.Add((keys[i], record));
        }

        return rows;
    }

    /// <summary>The records of a table whose records hold their own key in an <c>id</c>.</summary>
    /// <exception cref="InvalidDataException">A key names a record of another id.</exception>
    public static IReadOnlyList<Fields> ById(string name, IReadOnlyList<(long Key, Fields Record)> rows)
    {
        var records = new List<Fields>(rows.Count);

        foreach (var (key, record) in rows)
        {
            var id = record.Integer("id");

            if (key != id)
            {
                throw new InvalidDataException($"The {name} table keys record {id} as {key}.");
            }

            records.Add(record);
        }

        return records;
    }
}
