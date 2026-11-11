using System.Numerics;

namespace D3B.Extractor.Unity;

/// <summary>The plain fields of a serialized object, read by name through its type tree, each with its type kept.</summary>
/// <remarks>
/// Game data records are many small classes: reading their numbers, flags, strings, references and arrays into one bag
/// spares a reader per class, while a getter still fails, with the field's name, when the client removes or retypes a
/// field. A struct nests a bag of its own; an array of structs is a list of them.
/// </remarks>
internal sealed class Fields
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    // A field and a property's backing field of one name: an error only for a getter that asks for it.
    private readonly HashSet<string> _ambiguous = new(StringComparer.Ordinal);
    private readonly string _owner;

    private Fields(string owner)
    {
        _owner = owner;
    }

    /// <summary>Reads the fields of a node, the object itself for node 0, with the reader at its first field.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="node">The node.</param>
    /// <param name="owner">What the fields belong to, for errors: a class name.</param>
    public static Fields Read(ref ObjectReader reader, int node, string owner)
    {
        var tree = reader.Tree;
        var fields = new Fields(owner);

        foreach (var child in tree[node].Children)
        {
            var field = tree[child];
            var value = ReadValue(ref reader, child, $"{owner}.{field.Field}");

            if (value is not null && !fields._values.TryAdd(field.Field, value))
            {
                fields._ambiguous.Add(field.Field);
            }
        }

        reader.AlignAfter(node);

        return fields;
    }

    private static object? ReadValue(ref ObjectReader reader, int node, string owner)
    {
        var tree = reader.Tree;
        var field = tree[node];

        // A registry inside a reference type's tree is never written there: the object's own registry holds them all.
        if (field.IsRegistry)
        {
            return null;
        }

        switch (field.Type)
        {
            case "float":
                return reader.ReadSingle(node);
            case "int" or "SInt32":
                return reader.ReadInt32(node);
            case "unsigned int" or "UInt32":
                return reader.ReadUInt32(node);
            case "SInt64" or "long long":
                return reader.ReadInt64(node);
            case "UInt64" or "unsigned long long":
                return reader.ReadUInt64(node);
            case "SInt16" or "short":
                return reader.ReadInt16(node);
            case "UInt16" or "unsigned short":
                return reader.ReadUInt16(node);
            case "SInt8":
                return reader.ReadSByte(node);

            // The client stores flags and small numbers alike as UInt8: the getter says which it is.
            case "UInt8":
                return reader.ReadByte(node);
            case "bool":
                return reader.ReadBoolean(node);
            case "string":
                return reader.ReadString(node);
            case "AleVector2" or "Vector2f":
                return reader.ReadVector2(node);
            case "AleVector4" or "Vector4f":
                return reader.ReadVector4(node);
            case "managedReference":
                return reader.ReadReference(node);
        }

        var array = ArrayNode(tree, node);

        if (array is -1)
        {
            if (field.Children.Length > 0)
            {
                return Read(ref reader, node, owner);
            }

            reader.Skip(node);

            return null;
        }

        var item = tree[tree[array].Children[1]];

        switch (item.Type)
        {
            case "string":
                return reader.ReadStringArray(node);
            case "float":
                return reader.ReadSingleArray(node);
            case "int" or "SInt32" or "unsigned int" or "UInt32":
                return reader.ReadInt32Array(node);
        }

        if (item.Children.Length > 0)
        {
            return ReadItems(ref reader, node, owner);
        }

        // An array of another primitive, such as bytes, is not read by any table yet.
        reader.Skip(node);

        return null;
    }

    /// <summary>The array node of a field: the field itself, or its child for vectors; -1 for a field that holds none.</summary>
    private static int ArrayNode(TypeTree tree, int node)
    {
        if (tree[node].IsArray)
        {
            return node;
        }

        foreach (var child in tree[node].Children)
        {
            if (tree[child].IsArray)
            {
                return child;
            }
        }

        return -1;
    }

    private static List<Fields> ReadItems(ref ObjectReader reader, int node, string owner)
    {
        var item = reader.BeginArray(node, out var count);
        var items = new List<Fields>(count);

        for (var i = 0; i < count; i++)
        {
            items.Add(Read(ref reader, item, owner));
        }

        reader.EndArray(node);

        return items;
    }

    /// <summary>Gets whether the object has a field, which some layouts of a class lack.</summary>
    public bool Has(string name)
    {
        return _values.ContainsKey(name);
    }

    /// <summary>Gets an integer of any width, signed or not, such as a key one table stores in 16 bits and another in 32.</summary>
    public long Integer(string name)
    {
        var value = Value(name);

        return value switch
        {
            int number => number,
            uint number => number,
            short number => number,
            ushort number => number,
            sbyte number => number,
            byte number => number,
            long number => number,
            _ => throw Mistyped(name, value, "an integer"),
        };
    }

    public int Int32(string name)
    {
        return Get<int>(name);
    }

    public uint UInt32(string name)
    {
        return Get<uint>(name);
    }

    public long Int64(string name)
    {
        return Get<long>(name);
    }

    public ulong UInt64(string name)
    {
        return Get<ulong>(name);
    }

    public short Int16(string name)
    {
        return Get<short>(name);
    }

    public ushort UInt16(string name)
    {
        return Get<ushort>(name);
    }

    public sbyte SByte(string name)
    {
        return Get<sbyte>(name);
    }

    /// <summary>Gets a <c>UInt8</c> read as a number.</summary>
    public byte Byte(string name)
    {
        return Get<byte>(name);
    }

    public float Single(string name)
    {
        return Get<float>(name);
    }

    /// <summary>Gets a flag, which the client stores as a <c>bool</c> or as a <c>UInt8</c>.</summary>
    public bool Boolean(string name)
    {
        var value = Value(name);

        return value switch
        {
            bool flag => flag,
            byte flag when flag is 0 or 1 => flag is 1,
            _ => throw Mistyped(name, value, "a flag"),
        };
    }

    public string String(string name)
    {
        return Get<string>(name);
    }

    /// <summary>Gets an array of 32-bit integers, signed or not.</summary>
    public int[] Int32Array(string name)
    {
        return Get<int[]>(name);
    }

    public float[] SingleArray(string name)
    {
        return Get<float[]>(name);
    }

    public string[] StringArray(string name)
    {
        return Get<string[]>(name);
    }

    public Vector2 Vector2(string name)
    {
        return Get<Vector2>(name);
    }

    public Vector4 Vector4(string name)
    {
        return Get<Vector4>(name);
    }

    /// <summary>Gets the identifier of a managed reference in the object's registry.</summary>
    public long Reference(string name)
    {
        return Get<long>(name);
    }

    /// <summary>Gets an array of structs.</summary>
    public IReadOnlyList<Fields> Items(string name)
    {
        return Get<IReadOnlyList<Fields>>(name);
    }

    /// <summary>Gets the fields of a struct field.</summary>
    public Fields Nested(string name)
    {
        return Get<Fields>(name);
    }

    private object Value(string name)
    {
        if (_ambiguous.Contains(name))
        {
            throw new InvalidDataException($"{_owner} has two fields named {name}: which one the extractor should read is not known.");
        }

        return _values.TryGetValue(name, out var value)
            ? value
            : throw new InvalidDataException($"{_owner} has no field {name}: the client changed this type, so the extractor needs updating.");
    }

    private T Get<T>(string name)
    {
        var value = Value(name);

        return value is T typed ? typed : throw Mistyped(name, value, $"a {typeof(T).Name}");
    }

    private InvalidDataException Mistyped(string name, object value, string expected)
    {
        return new InvalidDataException($"{_owner}.{name} is a {value.GetType().Name}, where the extractor expects {expected}: the client changed this type, so the extractor needs updating.");
    }
}
