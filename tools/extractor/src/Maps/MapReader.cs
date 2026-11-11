using System.Globalization;
using D3B.Extractor.Unity;

namespace D3B.Extractor.Maps;

/// <summary>Reads one map of <c>Content/Map/Data</c>, in either of the client's two layouts (<c>main:docs/client.md</c>, "Maps").</summary>
/// <remarks>
/// <c>ClientMapData</c> holds its id, neighbours, cells and elements; <c>MapMetadata</c> nests the same in <c>mapData</c>
/// and takes its id from its name, <c>map_&lt;id&gt;</c>. An interactive element is any referenced object carrying an
/// interactive identifier, <c>interactiveId</c> or <c>m_interactionId</c>, whatever its class; in <c>ClientMapData</c>
/// one without a cell of its own stands on the cell of the middleground group that lists it.
/// </remarks>
internal sealed class MapReader
{
    private const string MapNamePrefix = "map_";
    private const int NoCell = -1;

    private readonly IReadOnlyDictionary<string, TypeTree> _references;
    private readonly HashSet<string> _interactiveClasses;
    private readonly Dictionary<long, int> _groupCells = [];
    private readonly Dictionary<long, (int Id, int CellId, int GfxId)> _referenced = [];
    private readonly List<(int Id, int CellId, int GfxId)> _elements = [];

    /// <summary>Gets the reference types of the file this reader reads the maps of.</summary>
    public IReadOnlyDictionary<string, TypeTree> References => _references;

    public MapReader(IReadOnlyDictionary<string, TypeTree> references)
    {
        _references = references;
        _interactiveClasses = new HashSet<string>(
            references.Where(reference => reference.Value[0].Children.Any(child => IsInteractiveField(reference.Value[child].Field))).Select(reference => reference.Key),
            StringComparer.Ordinal);
    }

    /// <summary>Tells whether an object of this tree is a map: the bundles also hold the elements' definitions, which are not.</summary>
    public static bool IsMap(TypeTree tree)
    {
        if (tree.Child(0, "id") is not -1 && tree.Child(0, "cellsData") is not -1 && tree.Child(0, "references") is not -1)
        {
            return true;
        }

        var mapData = tree.Child(0, "mapData");

        return mapData is not -1 && tree.Child(mapData, "cellsData") is not -1 && tree.Child(0, "references") is not -1;
    }

    /// <summary>Reads a map.</summary>
    /// <exception cref="InvalidDataException">The map is not laid out as the extractor measured.</exception>
    public MapData Read(SerializedObject map)
    {
        _groupCells.Clear();
        _referenced.Clear();
        _elements.Clear();

        var tree = map.Tree;
        var reader = new ObjectReader(map.Data, tree);
        var record = new Record();
        var nested = false;

        foreach (var child in tree[0].Children)
        {
            switch (tree[child].Field)
            {
                case "mapData":
                    nested = true;

                    foreach (var part in tree[child].Children)
                    {
                        ReadField(ref reader, part, record);
                    }

                    reader.AlignAfter(child);
                    break;
                case "references":
                    ReferenceRegistry.Read(ref reader, map.Data, child, _references, ReadReference);
                    break;
                default:
                    ReadField(ref reader, child, record);
                    break;
            }
        }

        reader.ExpectEnd();

        var id = nested ? IdOf(map.Name) : record.Id;

        return new MapData
        {
            Id = id,
            Top = record.Top,
            Bottom = record.Bottom,
            Left = record.Left,
            Right = record.Right,
            Flags = record.Flags ?? throw new InvalidDataException($"Map {id} has no cells."),
            LinkedZone = record.LinkedZone!,
            Numbers = record.Numbers,
            Elements = Elements(id, nested, record.InteractiveReferences),
        };
    }

    private static bool IsInteractiveField(string field)
    {
        return field is "interactiveId" or "m_interactionId";
    }

    private static int IdOf(string name)
    {
        if (!name.StartsWith(MapNamePrefix, StringComparison.Ordinal)
            || !int.TryParse(name.AsSpan(MapNamePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            throw new InvalidDataException($"A MapMetadata map is named '{name}', not map_<id>, which is where its id comes from.");
        }

        return id;
    }

    private static void ReadCells(ref ObjectReader reader, int node, Record record)
    {
        var tree = reader.Tree;
        var item = reader.BeginArray(node, out var count);

        if (count is not MapData.CellCount)
        {
            throw new InvalidDataException($"A map has {count} cells, where the extractor expects {MapData.CellCount}.");
        }

        var children = tree[item].Children;
        var names = children.Select(child => tree[child].Field).ToArray();
        var bits = names.Select(name => CellFlags.Names.ToList().IndexOf(name)).ToArray();
        var columns = names.Select(name => CellFlags.Numbers.GetValueOrDefault(name)).ToArray();

        foreach (var required in (string[])["cellNumber", "mapChangeData", "linkedZone", "mov", "los"])
        {
            if (!names.Contains(required))
            {
                throw new InvalidDataException($"{tree.Name}: a cell has no {required}: the client changed this type, so the extractor needs updating.");
            }
        }

        record.Flags = new ushort[count];
        record.LinkedZone = new byte[count];

        for (var cell = 0; cell < count; cell++)
        {
            for (var k = 0; k < children.Length; k++)
            {
                var child = children[k];
                var bit = bits[k];

                if (names[k] is "cellNumber")
                {
                    if (reader.ReadUInt32(child) != cell)
                    {
                        throw new InvalidDataException($"{tree.Name}: cell {cell} is numbered otherwise; the extractor expects cells in order.");
                    }
                }
                else if (names[k] is "linkedZone")
                {
                    record.LinkedZone[cell] = checked((byte)reader.ReadInt32(child));
                }
                else if (bit >= 0)
                {
                    if (reader.ReadBoolean(child))
                    {
                        record.Flags[cell] |= (ushort)(1 << bit);
                    }
                }
                else if (columns[k] is { } column)
                {
                    var value = reader.ReadInt32(child);

                    if (value is not 0)
                    {
                        record.Number(column).Add(cell, value);
                    }
                }
                else
                {
                    reader.Skip(child);
                }
            }

            reader.AlignAfter(item);
        }

        reader.EndArray(node);
    }

    private static (int Id, int CellId, int GfxId)? ReadElement(ref ObjectReader reader, int node)
    {
        var tree = reader.Tree;
        int? id = null;
        int? gfxId = null;
        var cell = NoCell;

        foreach (var child in tree[node].Children)
        {
            var field = tree[child].Field;

            if (IsInteractiveField(field))
            {
                id = checked((int)reader.ReadUInt32(child));
            }
            else if (field is "cellId")
            {
                cell = reader.ReadInt32(child);
            }
            else if (field is "gfxId")
            {
                gfxId = reader.ReadInt32(child);
            }
            else
            {
                reader.Skip(child);
            }
        }

        // The bot tells a map's resources by its elements' graphics: an element drawn with none is a layout not known.
        if (id is { } element && gfxId is null)
        {
            throw new InvalidDataException($"{tree.Name}: interactive element {element} names no graphic: the client changed this type, so the extractor needs updating.");
        }

        return id is { } found ? (found, cell, gfxId!.Value) : null;
    }

    private void ReadField(ref ObjectReader reader, int node, Record record)
    {
        switch (reader.Tree[node].Field)
        {
            case "id":
                record.Id = reader.ReadInt32(node);
                break;
            case "topNeighbourId":
                record.Top = reader.ReadInt32(node);
                break;
            case "bottomNeighbourId":
                record.Bottom = reader.ReadInt32(node);
                break;
            case "leftNeighbourId":
                record.Left = reader.ReadInt32(node);
                break;
            case "rightNeighbourId":
                record.Right = reader.ReadInt32(node);
                break;
            case "cellsData":
                ReadCells(ref reader, node, record);
                break;
            case "middlegroundMapElements":
                ReadGroupCells(ref reader, node);
                break;
            case "interactiveElements":
                record.InteractiveReferences = reader.ReadReferenceArray(node);
                break;
            case "boundingBoxes":
                ReadBoundingBoxes(ref reader, node);
                break;
            default:
                reader.Skip(node);
                break;
        }
    }

    private void ReadGroupCells(ref ObjectReader reader, int node)
    {
        var tree = reader.Tree;
        var item = reader.BeginArray(node, out var count);

        for (var i = 0; i < count; i++)
        {
            long[] elements = [];
            var cell = NoCell;

            foreach (var child in tree[item].Children)
            {
                switch (tree[child].Field)
                {
                    case "mapElements":
                        elements = reader.ReadReferenceArray(child);
                        break;
                    case "cellId":
                        cell = reader.ReadInt32(child);
                        break;
                    default:
                        reader.Skip(child);
                        break;
                }
            }

            reader.AlignAfter(item);

            foreach (var element in elements)
            {
                _groupCells[element] = cell;
            }
        }

        reader.EndArray(node);
    }

    private void ReadBoundingBoxes(ref ObjectReader reader, int node)
    {
        var item = reader.BeginArray(node, out var count);

        for (var i = 0; i < count; i++)
        {
            var element = ReadElement(ref reader, item);

            reader.AlignAfter(item);

            if (element is { } found)
            {
                _elements.Add(found);
            }
        }

        reader.EndArray(node);
    }

    private bool ReadReference(long id, string className, ref ObjectReader reader)
    {
        if (!_interactiveClasses.Contains(className))
        {
            return false;
        }

        if (ReadElement(ref reader, 0) is { } element)
        {
            _referenced[id] = element;
        }

        return true;
    }

    private List<(int Id, int CellId, int GfxId)> Elements(int mapId, bool nested, long[] references)
    {
        if (nested)
        {
            foreach (var reference in references)
            {
                _elements.Add(_referenced.TryGetValue(reference, out var element)
                    ? element
                    : throw new InvalidDataException($"Map {mapId} lists interactive element {reference}, which its registry does not hold."));
            }
        }
        else
        {
            foreach (var (reference, element) in _referenced)
            {
                var hasGroup = _groupCells.TryGetValue(reference, out var group);

                if (element.CellId is not NoCell && hasGroup && group != element.CellId)
                {
                    throw new InvalidDataException($"Map {mapId}: interactive element {element.Id} names cell {element.CellId}, its group cell {group}.");
                }

                _elements.Add((element.Id, element.CellId is not NoCell ? element.CellId : hasGroup ? group : NoCell, element.GfxId));
            }
        }

        _elements.Sort();

        var result = new List<(int Id, int CellId, int GfxId)>(_elements.Count);

        foreach (var element in _elements)
        {
            // An element drawn twice on one cell is one element, kept with its lowest graphic; on two cells, the map is not known.
            if (result.Count > 0 && result[^1].Id == element.Id)
            {
                if (result[^1].CellId != element.CellId)
                {
                    throw new InvalidDataException($"Map {mapId} places interactive element {element.Id} on cells {result[^1].CellId} and {element.CellId}.");
                }

                continue;
            }

            result.Add(element);
        }

        return result;
    }

    private sealed class Record
    {
        public int Id { get; set; }

        public int Top { get; set; }

        public int Bottom { get; set; }

        public int Left { get; set; }

        public int Right { get; set; }

        public ushort[]? Flags { get; set; }

        public byte[]? LinkedZone { get; set; }

        public Dictionary<string, SortedDictionary<int, int>> Numbers { get; } = new(StringComparer.Ordinal);

        public long[] InteractiveReferences { get; set; } = [];

        public SortedDictionary<int, int> Number(string column)
        {
            if (!Numbers.TryGetValue(column, out var cells))
            {
                cells = [];
                Numbers.Add(column, cells);
            }

            return cells;
        }
    }
}
