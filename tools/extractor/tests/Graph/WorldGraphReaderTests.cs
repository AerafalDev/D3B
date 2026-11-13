using D3B.Extractor.Graph;
using static D3B.Extractor.Tests.Graph.TestGraph;

namespace D3B.Extractor.Tests.Graph;

/// <summary>The world graph's reader on graphs built by hand, so that CI covers its checks without a client.</summary>
public sealed class WorldGraphReaderTests
{
    private const long Astrub = 54164787;
    private const long North = 54164788;

    private static readonly TestGraph _graph = Of(
        [(11, North, 1), (10, Astrub, 1), (12, North, 2)],
        [
            (12, 10, [Scroll(Astrub, 2, 540)]),
            (10, 11, [Scroll(North, 6, 6), Use(North, 300, 184, 465453, "PZ=1")]),
            (10, 12, [MapAction(North, 30)]),
        ]);

    [Fact]
    public void AGraphIsReadWithItsVerticesAndEdgesInOrder()
    {
        var graph = WorldGraphReader.Read(_graph.Build());

        Assert.Equal([(10UL, Astrub, 1), (11UL, North, 1), (12UL, North, 2)], graph.Vertices);
        Assert.Equal([(10UL, 11UL), (10UL, 12UL), (12UL, 10UL)], graph.Edges.Select(edge => (edge.From, edge.To)));

        var (scroll, use) = (graph.Edges[0].Transitions[0], graph.Edges[0].Transitions[1]);

        // Only an interactive transition names a skill and an element; an empty criterion is none.
        Assert.Equal(("scroll", 6, 6, (int?)null, North, (int?)null, (string?)null), (scroll.Type, scroll.Direction, scroll.CellId, scroll.SkillId, scroll.MapId, scroll.ElementId, scroll.Criterion));
        Assert.Equal(("interactive", 255, 300, (int?)184, North, (int?)465453, "PZ=1"), (use.Type, use.Direction, use.CellId, use.SkillId, use.MapId, use.ElementId, use.Criterion));
        Assert.Equal("mapAction", graph.Edges[1].Transitions[0].Type);
    }

    [Fact]
    public void ADictionaryOfAnotherShapeIsRefused()
    {
        Refused(_graph with { ThirdPart = true }, "a dictionary of m_keys and m_values");
    }

    [Fact]
    public void KeysAndValuesOfDifferentCountsAreRefused()
    {
        Refused(_graph with { MapKeys = [Astrub, North, 1] }, "3 keys for 2 values");
    }

    [Fact]
    public void ZonesAndVerticesOfDifferentCountsAreRefused()
    {
        Refused(_graph with { Maps = [(Astrub, [1, 2], [(Astrub, 1, 10)])] }, "has 2 zones for 1 vertices");
    }

    [Theory]
    [InlineData(North, 1)]
    [InlineData(Astrub, 2)]
    public void AVertexKeyedUnderAnotherMapOrZoneIsRefused(long map, int zone)
    {
        Refused(_graph with { Maps = [(Astrub, [1], [(map, zone, 10)])], Sources = [] }, $"names map {map}, zone {zone}");
    }

    [Fact]
    public void TwoVerticesOfOneUidAreRefused()
    {
        Refused(Of([(10, Astrub, 1), (10, North, 1)], []), "two vertices of uid 10");
    }

    [Fact]
    public void DestinationsAndEdgesOfDifferentCountsAreRefused()
    {
        Refused(_graph with { Sources = [(10, [11, 12], [(10, 11, [Scroll(North, 6, 6)])])] }, "has 2 destinations for 1 edges");
    }

    [Fact]
    public void AnEdgeKeyedUnderOtherVerticesIsRefused()
    {
        Refused(_graph with { Sources = [(10, [11], [(12, 11, [Scroll(North, 6, 6)])])] }, "from 10 to 11 names 12 to 11");
    }

    [Fact]
    public void AnEdgeToAVertexTheGraphLacksOrWithoutTransitionIsRefused()
    {
        Refused(_graph with { Sources = [(10, [99], [(10, 99, [Scroll(North, 6, 6)])])] }, "names a vertex it lacks, or has no transition");
        Refused(_graph with { Sources = [(10, [11], [(10, 11, [])])] }, "names a vertex it lacks, or has no transition");
    }

    [Fact]
    public void ATransitionOfAnUnknownTypeIsRefused()
    {
        Refused(WithTransition(Scroll(North, 6, 6) with { Type = 4 }), "has type 4, which the extractor does not know");
    }

    [Theory]
    [InlineData(-1, 6)]
    [InlineData(8, 6)]
    [InlineData(6, -1)]
    [InlineData(6, 560)]
    public void ADirectionOrACellOutOfRangeIsRefused(int direction, int cell)
    {
        Refused(WithTransition(Scroll(North, direction, cell)), $"names direction {direction} and cell {cell}");
    }

    [Theory]
    [InlineData(184, -1L)]
    [InlineData(-1, 465453L)]
    public void ATransitionThatIsNotInteractiveNamingASkillOrAnElementIsRefused(int skill, long element)
    {
        Refused(WithTransition(Scroll(North, 6, 6) with { Skill = skill, Element = element }), $"names skill {skill} and element {element}");
    }

    [Theory]
    [InlineData(-1, 465453L)]
    [InlineData(184, -1L)]
    [InlineData(184, 1L + int.MaxValue)]
    public void AnInteractiveTransitionWithoutASkillOrAnElementIsRefused(int skill, long element)
    {
        Refused(WithTransition(Use(North, 300, skill, element, "")), $"names skill {skill} and element {element}");
    }

    [Fact]
    public void BytesLeftAfterTheGraphAreRefused()
    {
        Refused(_graph with { Trailing = [0, 0, 0, 0] }, "4 bytes are left");
    }

    private static TestGraph WithTransition((int Type, int Direction, int Cell, int Skill, long Map, long Element, string Criterion) transition)
    {
        return _graph with { Sources = [(10, [11], [(10, 11, [transition])])] };
    }

    private static void Refused(TestGraph graph, string reason)
    {
        var built = graph.Build();
        var error = Assert.Throws<InvalidDataException>(() => WorldGraphReader.Read(built));

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }
}
