namespace D3B.Extractor.Unity;

/// <summary>The type tree of a serialized type, with the children of each node, to read objects without building them.</summary>
/// <remarks>
/// AssetsTools.NET's generic reading builds one object per field, too slow for the 4 GiB of maps (<c>main:docs/client.md</c>).
/// Reading through the type tree walks the bytes, reads the fields asked for by name and type, and skips the others: a
/// field the client adds changes nothing, a field it removes or retypes fails loudly.
/// </remarks>
internal sealed class TypeTree
{
    private const string BackingFieldPrefix = "<";

    private const string BackingFieldSuffix = ">k__BackingField";

    private readonly TypeNode[] _nodes;

    /// <summary>Gets the type's name, for errors.</summary>
    public string Name { get; }

    /// <summary>Gets the number of nodes.</summary>
    public int Count => _nodes.Length;

    /// <summary>Gets a node by its index; the root is 0.</summary>
    public TypeNode this[int index] => _nodes[index];

    private TypeTree(string name, TypeNode[] nodes)
    {
        Name = name;
        _nodes = nodes;
    }

    /// <summary>Builds the tree of nodes stored in depth-first order.</summary>
    /// <exception cref="InvalidDataException">The nodes do not form one tree.</exception>
    public static TypeTree Build(string name, IReadOnlyList<FlatNode> source)
    {
        if (source.Count is 0 || source[0].Level is not 0)
        {
            throw new InvalidDataException($"The type tree of {name} has no root.");
        }

        var children = new List<int>[source.Count];
        var parents = new Stack<int>();

        for (var i = 0; i < source.Count; i++)
        {
            children[i] = [];

            while (parents.Count > 0 && source[parents.Peek()].Level >= source[i].Level)
            {
                parents.Pop();
            }

            if (i > 0)
            {
                if (parents.Count is 0)
                {
                    throw new InvalidDataException($"The type tree of {name} has a second root at node {i}.");
                }

                if (source[parents.Peek()].Level != source[i].Level - 1)
                {
                    throw new InvalidDataException($"The type tree of {name} skips a level at node {i}.");
                }

                children[parents.Peek()].Add(i);
            }

            parents.Push(i);
        }

        var nodes = new TypeNode[source.Count];

        for (var i = 0; i < source.Count; i++)
        {
            var node = source[i];

            nodes[i] = new TypeNode
            {
                Type = node.Type,
                Name = node.Name,
                Field = FieldOf(node.Name),
                ByteSize = node.ByteSize,
                Align = node.Align,
                IsArray = node.IsArray,
                IsRegistry = node.IsRegistry,
                Children = [.. children[i]],
            };

            // An array's first child is its size and its second the item: a reader trusts that shape.
            if (node.IsArray && children[i].Count is not 2)
            {
                throw new InvalidDataException($"The type tree of {name} has an array, {node.Name}, without a size and an item.");
            }
        }

        return new TypeTree(name, nodes);
    }

    private static string FieldOf(string name)
    {
        return name.StartsWith(BackingFieldPrefix, StringComparison.Ordinal) && name.EndsWith(BackingFieldSuffix, StringComparison.Ordinal)
            ? name[BackingFieldPrefix.Length..^BackingFieldSuffix.Length]
            : name;
    }

    /// <summary>Finds a child by its serialized name, or by the name of the property it backs.</summary>
    /// <returns>Its index, or -1.</returns>
    /// <exception cref="InvalidDataException">Two children answer to the name: a field and a property's backing field.</exception>
    public int Child(int node, string name)
    {
        var found = -1;

        foreach (var child in _nodes[node].Children)
        {
            if (_nodes[child].Field != name && _nodes[child].Name != name)
            {
                continue;
            }

            if (found is not -1)
            {
                throw new InvalidDataException($"{Name}.{_nodes[node].Name} has two fields named {name}: which one the extractor should read is not known.");
            }

            found = child;
        }

        return found;
    }

    /// <summary>Finds a child that must be there.</summary>
    /// <exception cref="InvalidDataException">The node has no such child.</exception>
    public int Required(int node, string name)
    {
        var child = Child(node, name);

        if (child is -1)
        {
            throw new InvalidDataException($"{Name}.{_nodes[node].Name} has no field {name}: the client changed this type, so the extractor needs updating.");
        }

        return child;
    }

    /// <summary>The array node of a field: the field itself, or its child for vectors and strings.</summary>
    /// <exception cref="InvalidDataException">The field holds no array.</exception>
    public int ArrayOf(int node)
    {
        if (_nodes[node].IsArray)
        {
            return node;
        }

        foreach (var child in _nodes[node].Children)
        {
            if (_nodes[child].IsArray)
            {
                return child;
            }
        }

        throw Unexpected(node, "an array");
    }

    /// <summary>The error for a field whose type is not the one the extractor reads.</summary>
    public InvalidDataException Unexpected(int node, string expected)
    {
        var field = _nodes[node];

        return new InvalidDataException($"{Name}.{field.Name} is a {field.Type}, where the extractor expects {expected}: the client changed this type, so the extractor needs updating.");
    }
}
