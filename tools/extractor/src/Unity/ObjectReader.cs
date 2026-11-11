using System.Buffers.Binary;
using System.Text;

namespace D3B.Extractor.Unity;

/// <summary>Reads the bytes of a serialized object field by field, as its <see cref="TypeTree"/> lays them out.</summary>
/// <remarks>Every typed read checks the field's type first, so that a change in the client fails instead of misreading.</remarks>
internal ref struct ObjectReader
{
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>Gets or sets where the next read starts.</summary>
    public int Position { get; set; }

    /// <summary>Gets the object's length in bytes.</summary>
    public readonly int Length => _data.Length;

    /// <summary>Gets the tree that lays the object out.</summary>
    public TypeTree Tree { get; }

    public ObjectReader(ReadOnlySpan<byte> data, TypeTree tree)
    {
        _data = data;
        Tree = tree;
    }

    /// <summary>Moves past a field without reading it.</summary>
    public void Skip(int node)
    {
        var field = Tree[node];

        // A registry inside a reference type's tree is not serialized there: the object's own registry holds them all.
        if (field.IsRegistry)
        {
            return;
        }

        if (field.IsArray)
        {
            var element = field.Children[1];
            var item = Tree[element];

            if (item.Children.Length is 0 && !item.Align && item.ByteSize > 0)
            {
                Take(checked(Count(item.ByteSize) * item.ByteSize));
            }
            else
            {
                // Items of no fixed size are skipped one by one: running out of bytes is caught there.
                var count = RawInt32();

                if (count < 0)
                {
                    throw new InvalidDataException($"{Tree.Name}: an array claims {count} items.");
                }

                for (var i = 0; i < count; i++)
                {
                    Skip(element);
                }
            }
        }
        else if (field.Children.Length > 0)
        {
            foreach (var child in field.Children)
            {
                Skip(child);
            }
        }
        else
        {
            if (field.ByteSize < 0)
            {
                throw new InvalidDataException($"{Tree.Name}.{field.Name} is a {field.Type} of no fixed size, which cannot be skipped.");
            }

            Take(field.ByteSize);
        }

        AlignAfter(node);
    }

    public int ReadInt32(int node)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(Primitive(node, 4, "int", "SInt32"));
    }

    public uint ReadUInt32(int node)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(Primitive(node, 4, "unsigned int", "UInt32"));
    }

    public long ReadInt64(int node)
    {
        return BinaryPrimitives.ReadInt64LittleEndian(Primitive(node, 8, "SInt64", "long long"));
    }

    public ulong ReadUInt64(int node)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(Primitive(node, 8, "UInt64", "unsigned long long"));
    }

    public short ReadInt16(int node)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(Primitive(node, 2, "SInt16", "short"));
    }

    public ushort ReadUInt16(int node)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(Primitive(node, 2, "UInt16", "unsigned short"));
    }

    public sbyte ReadSByte(int node)
    {
        return (sbyte)Primitive(node, 1, "SInt8", "char")[0];
    }

    /// <summary>Reads an unsigned byte as a number: the client stores both flags and small numbers as <c>UInt8</c>.</summary>
    public byte ReadByte(int node)
    {
        return Primitive(node, 1, "UInt8", "unsigned char")[0];
    }

    public float ReadSingle(int node)
    {
        return BinaryPrimitives.ReadSingleLittleEndian(Primitive(node, 4, "float", "float"));
    }

    /// <summary>Reads a flag, which the client stores as a <c>bool</c> or as a <c>UInt8</c>.</summary>
    public bool ReadBoolean(int node)
    {
        return Primitive(node, 1, "bool", "UInt8")[0] is not 0;
    }

    public string ReadString(int node)
    {
        if (Tree[node].Type is not "string")
        {
            throw Tree.Unexpected(node, "a string");
        }

        var array = Tree.ArrayOf(node);
        var text = Encoding.UTF8.GetString(Take(Count(1)));

        AlignAfter(array);
        AlignAfter(node);

        return text;
    }

    /// <summary>Moves to a four-byte boundary when the node asks for it.</summary>
    public void AlignAfter(int node)
    {
        if (Tree[node].Align)
        {
            Align();
        }
    }

    /// <summary>Moves to the next four-byte boundary.</summary>
    public void Align()
    {
        Position = (Position + 3) & ~3;
    }

    /// <summary>Checks that the whole object was read, which a misread layout rarely achieves.</summary>
    /// <exception cref="InvalidDataException">Bytes are left.</exception>
    public readonly void ExpectEnd()
    {
        if (Position > _data.Length)
        {
            throw new InvalidDataException($"{Tree.Name}: the object ends early.");
        }

        if (Position != _data.Length)
        {
            throw new InvalidDataException($"{Tree.Name}: {_data.Length - Position} bytes are left after reading the object, so its layout was misread.");
        }
    }

    /// <summary>Reads a 32-bit integer that has no node of its own.</summary>
    public int RawInt32()
    {
        return BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    }

    /// <summary>Reads a 64-bit integer that has no node of its own.</summary>
    public long RawInt64()
    {
        return BinaryPrimitives.ReadInt64LittleEndian(Take(8));
    }

    private ReadOnlySpan<byte> Primitive(int node, int size, string type, string alternative)
    {
        var actual = Tree[node].Type;

        if (actual != type && actual != alternative)
        {
            throw Tree.Unexpected(node, type);
        }

        var value = Take(size);

        AlignAfter(node);

        return value;
    }

    /// <summary>Reads the count of an array, checked against the bytes left before anything is allocated for it.</summary>
    private int Count(int itemSize)
    {
        var count = RawInt32();

        if (count < 0 || count > (_data.Length - Position) / itemSize)
        {
            throw new InvalidDataException($"{Tree.Name}: an array claims {count} items of at least {itemSize} bytes, with {_data.Length - Position} bytes left.");
        }

        return count;
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || count > _data.Length - Position)
        {
            throw new InvalidDataException($"{Tree.Name}: the object ends early.");
        }

        var slice = _data.Slice(Position, count);

        Position += count;

        return slice;
    }
}
