using D3B.Extractor.Graph;
using D3B.Extractor.Maps;
using D3B.Extractor.World;

namespace D3B.Extractor.Tests.Graph;

public sealed class GraphTablesTests
{
    private static readonly WorldGraph _graph = new()
    {
        Vertices = [(0, 54164787, 1), (1, 54164788, 1)],
        Edges =
        [
            (0, 1, [
                new GraphTransition { Type = "scroll", Direction = 6, CellId = 6, SkillId = null, MapId = 54164788, ElementId = null, Criterion = null },
                new GraphTransition { Type = "interactive", Direction = 255, CellId = 300, SkillId = 184, MapId = 54164788, ElementId = 465453, Criterion = "PZ=1" },
            ]),
        ],
    };

    [Fact]
    public void TheGraphIsWrittenAsTheAppReadsIt()
    {
        // The app's own tests read the same fixtures (src-tauri/src/data/graph.rs).
        Assert.Equal(Fixture("world-graph-vertices.json"), GraphTables.WriteVertices(_graph));
        Assert.Equal(Fixture("world-graph-edges.json"), GraphTables.WriteEdges(_graph));
    }

    [Fact]
    public void AGraphThatFitsItsMapsPasses()
    {
        Assert.Equal(0, GraphTables.Check(_graph, Maps(zone: 1, element: 465453), [], [new Zaap { Id = 1, MapId = 54164787, SubAreaId = 1, Activated = true }]));
    }

    [Fact]
    public void AVertexWhoseZoneNoWalkableCellHasFails()
    {
        Assert.Throws<InvalidDataException>(() => GraphTables.Check(_graph, Maps(zone: 2, element: 465453), [], []));
    }

    [Fact]
    public void ATransitionThroughAnElementTheMapLacksFails()
    {
        Assert.Throws<InvalidDataException>(() => GraphTables.Check(_graph, Maps(zone: 1, element: 1), [], []));
    }

    [Fact]
    public void AZaapOnAMapTheClientLacksFails()
    {
        Assert.Throws<InvalidDataException>(() => GraphTables.Check(_graph, Maps(zone: 1, element: 465453), [], [new Zaap { Id = 1, MapId = 9, SubAreaId = 1, Activated = true }]));
    }

    private static Dictionary<long, MapData> Maps(int zone, int element)
    {
        MapData Map(int id)
        {
            var flags = new ushort[MapData.CellCount];
            var zones = new byte[MapData.CellCount];

            flags[10] = 1;
            zones[10] = (byte)(zone << 4);

            return new MapData { Id = id, Top = 0, Bottom = 0, Left = 0, Right = 0, Flags = flags, LinkedZone = zones, Numbers = new Dictionary<string, SortedDictionary<int, int>>(), Elements = [(element, 300, 7506)] };
        }

        return new Dictionary<long, MapData> { [54164787] = Map(54164787), [54164788] = Map(54164788) };
    }

    private static byte[] Fixture(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "world", name));
    }
}
