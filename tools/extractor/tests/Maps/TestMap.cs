using D3B.Extractor.Maps;
using D3B.Extractor.Tests.Unity;
using D3B.Extractor.Unity;
using static D3B.Extractor.Tests.Unity.Trees;

namespace D3B.Extractor.Tests.Maps;

/// <summary>Builds a map object by hand, in either of the client's layouts, with its type tree and its bytes.</summary>
/// <remarks>
/// <c>ClientMapData</c> holds its id, neighbours, cells, middleground groups and registry; <c>MapMetadata</c> nests its
/// cells, listed elements and bounding boxes in <c>mapData</c>. The shapes are ours, written from the extractor's reader.
/// </remarks>
internal sealed class TestMap
{
    /// <summary>The reference classes every test map's file describes: interactive or not, with a cell or without, with a graphic or without.</summary>
    public static readonly IReadOnlyDictionary<string, TypeTree> ReferenceTypes = new Dictionary<string, TypeTree>(StringComparer.Ordinal)
    {
        ["Dofus.InteractiveElement"] = Reference("InteractiveElement", ("unsigned int", "interactiveId"), ("int", "cellId"), ("int", "gfxId")),
        ["Dofus.GroupedElement"] = Reference("GroupedElement", ("unsigned int", "interactiveId"), ("int", "gfxId")),
        ["Dofus.MapInteractive"] = Reference("MapInteractive", ("unsigned int", "m_interactionId"), ("int", "cellId"), ("int", "gfxId")),
        ["Dofus.Decor"] = Reference("Decor", ("int", "gfxId"), ("int", "cellId")),
        ["Dofus.Blank"] = Reference("Blank", ("unsigned int", "interactiveId")),
    };

    /// <summary>Gets whether the map is a <c>MapMetadata</c>, rather than a <c>ClientMapData</c>.</summary>
    public bool Nested { get; init; }

    /// <summary>Gets the object's name, where a <c>MapMetadata</c> takes its id.</summary>
    public string Name { get; init; } = "";

    public int Id { get; init; } = 191106007;

    public int Top { get; init; } = 191106008;

    public int Bottom { get; init; } = 191106006;

    public int Left { get; init; }

    public int Right { get; init; } = 191106520;

    /// <summary>Gets how many cells the map holds, or null for a map without <c>cellsData</c>.</summary>
    public int? Cells { get; init; } = MapData.CellCount;

    /// <summary>Gets a cell's fields in order, or null for the layout's own: a flag is a <c>bool</c>, any other an integer.</summary>
    public string[]? CellFields { get; init; }

    /// <summary>Gets the number a cell carries in <c>cellNumber</c>.</summary>
    public Func<int, int> CellNumber { get; init; } = cell => cell;

    /// <summary>Gets the value of a cell's field, a flag being set where it is not zero.</summary>
    public Func<int, string, int> Cell { get; init; } = (_, _) => 0;

    /// <summary>Gets the middleground groups of a <c>ClientMapData</c>: each its cell and the references it lists.</summary>
    public (int Cell, long[] Elements)[] Groups { get; init; } = [];

    /// <summary>Gets the references a <c>MapMetadata</c> lists as its interactive elements.</summary>
    public long[] Listed { get; init; } = [];

    /// <summary>Gets the bounding boxes of a <c>MapMetadata</c>: each its interactive id, graphic and cell.</summary>
    public (int Id, int GfxId, int CellId)[] Boxes { get; init; } = [];

    /// <summary>Gets the registry's objects: each its identifier, its class in <see cref="ReferenceTypes"/>, and its fields' values in order.</summary>
    public (long Rid, string Class, int[] Values)[] References { get; init; } = [];

    /// <summary>Gets bytes written after the object, which a reader must refuse.</summary>
    public byte[] Trailing { get; init; } = [];

    public static MapReader Reader()
    {
        return new MapReader(ReferenceTypes);
    }

    private static TypeTree Reference(string name, params (string Type, string Name)[] fields)
    {
        return TypeTree.Build(name, [Node(0, name, "Base"), .. fields.Select(field => Node(1, field.Type, field.Name, 4))]);
    }

    private static IEnumerable<FlatNode> ReferenceVector(int level, string name)
    {
        yield return Node(level, "vector", name);
        yield return Array(level + 1, align: true);
        yield return Node(level + 2, "int", "size", 4);
        yield return Node(level + 2, "managedReference", "data");
        yield return Node(level + 3, "SInt64", "rid", 8);
    }

    public SerializedObject Build()
    {
        var nodes = new List<FlatNode> { Node(0, Nested ? "MapMetadata" : "ClientMapData", "Base") };
        var bytes = new ObjectBytes();

        if (Nested)
        {
            nodes.Add(Node(1, "MapData", "mapData", align: true));
            AddCells(nodes, bytes, 2);
            nodes.AddRange(ReferenceVector(2, "interactiveElements"));
            bytes.Int32(Listed.Length);

            foreach (var reference in Listed)
            {
                bytes.Int64(reference);
            }

            AddBoxes(nodes, bytes, 2);
        }
        else
        {
            foreach (var (name, value) in (ReadOnlySpan<(string, int)>)[("id", Id), ("topNeighbourId", Top), ("bottomNeighbourId", Bottom), ("leftNeighbourId", Left), ("rightNeighbourId", Right), ("mapVersion", 11)])
            {
                nodes.Add(Node(1, "int", name, 4));
                bytes.Int32(value);
            }

            AddCells(nodes, bytes, 1);
            AddGroups(nodes, bytes, 1);
        }

        AddRegistry(nodes, bytes, 1);
        bytes.Raw(Trailing);

        return new SerializedObject { PathId = 1, Name = Name, Tree = TypeTree.Build("Map", nodes), Data = bytes.ToArray(), ReferenceTypes = ReferenceTypes };
    }

    private void AddCells(List<FlatNode> nodes, ObjectBytes bytes, int level)
    {
        if (Cells is not { } count)
        {
            return;
        }

        var fields = CellFields ?? ["cellNumber", "mapChangeData", "linkedZone", Nested ? "floor" : "altitude", "groundSlope", "mov", "los"];

        nodes.Add(Node(level, "vector", "cellsData"));
        nodes.Add(Array(level + 1, align: true));
        nodes.Add(Node(level + 2, "int", "size", 4));
        nodes.Add(Node(level + 2, "CellData", "data", align: true));
        nodes.AddRange(fields.Select(field => CellFlags.Names.Contains(field) ? Node(level + 3, "bool", field, 1)
            : Node(level + 3, field is "cellNumber" ? "unsigned int" : "int", field, 4)));
        bytes.Int32(count);

        for (var cell = 0; cell < count; cell++)
        {
            foreach (var field in fields)
            {
                var value = field is "cellNumber" ? CellNumber(cell) : Cell(cell, field);

                if (CellFlags.Names.Contains(field))
                {
                    bytes.Byte(value is 0 ? (byte)0 : (byte)1);
                }
                else
                {
                    bytes.Int32(value);
                }
            }

            bytes.Align();
        }
    }

    private void AddGroups(List<FlatNode> nodes, ObjectBytes bytes, int level)
    {
        nodes.Add(Node(level, "vector", "middlegroundMapElements"));
        nodes.Add(Array(level + 1, align: true));
        nodes.Add(Node(level + 2, "int", "size", 4));
        nodes.Add(Node(level + 2, "MiddlegroundGroup", "data", align: true));
        nodes.Add(Node(level + 3, "int", "cellId", 4));
        nodes.AddRange(ReferenceVector(level + 3, "mapElements"));
        bytes.Int32(Groups.Length);

        foreach (var (cell, elements) in Groups)
        {
            bytes.Int32(cell);
            bytes.Int32(elements.Length);

            foreach (var element in elements)
            {
                bytes.Int64(element);
            }
        }
    }

    private void AddBoxes(List<FlatNode> nodes, ObjectBytes bytes, int level)
    {
        nodes.Add(Node(level, "vector", "boundingBoxes"));
        nodes.Add(Array(level + 1, align: true));
        nodes.Add(Node(level + 2, "int", "size", 4));
        nodes.Add(Node(level + 2, "BoundingBox", "data", align: true));
        nodes.Add(Node(level + 3, "unsigned int", "m_interactionId", 4));
        nodes.Add(Node(level + 3, "int", "gfxId", 4));
        nodes.Add(Node(level + 3, "int", "cellId", 4));
        bytes.Int32(Boxes.Length);

        foreach (var (id, gfxId, cellId) in Boxes)
        {
            bytes.Int32(id);
            bytes.Int32(gfxId);
            bytes.Int32(cellId);
        }
    }

    private void AddRegistry(List<FlatNode> nodes, ObjectBytes bytes, int level)
    {
        nodes.Add(new FlatNode { Level = level, Type = "ManagedReferencesRegistry", Name = "references", ByteSize = -1, IsRegistry = true });
        nodes.Add(Node(level + 1, "int", "version", 4));
        nodes.Add(Node(level + 1, "vector", "RefIds"));
        nodes.Add(Array(level + 2));
        nodes.Add(Node(level + 3, "int", "size", 4));
        nodes.Add(Node(level + 3, "ReferencedObject", "data", align: true));
        nodes.Add(Node(level + 4, "SInt64", "rid", 8));
        nodes.Add(Node(level + 4, "ReferencedManagedType", "type"));
        nodes.AddRange(String(level + 5, "class"));
        nodes.AddRange(String(level + 5, "ns"));
        nodes.AddRange(String(level + 5, "asm"));
        bytes.Int32(2);
        bytes.Int32(References.Length);

        foreach (var (rid, className, values) in References)
        {
            var dot = className.LastIndexOf('.');

            bytes.Int64(rid);
            bytes.String(className[(dot + 1)..]);
            bytes.String(dot is -1 ? "" : className[..dot]);
            bytes.String("Assembly-CSharp");

            foreach (var value in values)
            {
                bytes.Int32(value);
            }

            bytes.Align();
        }
    }
}
