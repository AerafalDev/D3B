using System.Globalization;
using System.Text.Json.Nodes;
using D3B.Bot.Behaviors;
using D3B.Bot.Routes;
using D3B.Bot.Settings;

namespace D3B.Bot.Tests.Routes;

public sealed class RouteActionsTests : IAsyncDisposable
{
    private readonly Behaviors.TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    private NodeDefinition Node(string id)
    {
        Assert.True(_behaviors.Nodes.TryGet(id, out var definition));

        return definition;
    }

    [Fact]
    public void EveryActionTheEditorOffersIsANodeOfTheCatalogWithItsValuesAndSentenceInBothLanguages()
    {
        Assert.Equal(["route.harvest", "route.fight", "route.door", "route.exit", "wait", "route.zaap", "route.bag", "route.npc"], RouteActions.Offered);

        foreach (var id in RouteActions.Offered.Concat([RouteActions.Follow]))
        {
            var node = Node(id);

            Assert.Equal(NodeRole.Action, node.Role);

            foreach (var language in new[] { "en", "fr" })
            {
                var culture = CultureInfo.GetCultureInfo(language);

                Assert.DoesNotContain("[", node.Sentence.In(culture), StringComparison.Ordinal);
                Assert.NotEmpty(node.Help!.In(culture));
            }
        }
    }

    [Fact]
    public void EveryActionTheEditorOffersIsPlayedAndFollowingARouteIsNotAnActionOfOne()
    {
        Assert.Equal(RouteActions.Offered, RouteActions.Offered.Where(RouteActions.IsPlayed));
        Assert.False(RouteActions.IsPlayed(RouteActions.Follow));
    }

    [Fact]
    public void AnNpcActionHoldsTheNpcAndTheRepliesGivenToItInOrderAndNeedsTheNpc()
    {
        var node = Node(RouteActions.Npc);

        Assert.Equal([SettingKind.Npc, SettingKind.NpcReply], node.Values.All.Select(setting => setting.Kind));
        Assert.False(node.Values.All[0].IsList);
        Assert.True(node.Values.All[1].IsList);
        Assert.True(node.Values.All[1].IsOrdered);
        Assert.Equal(SettingProblemKind.Required, Assert.Single(node.Values.Check(new RouteNpcValues())).Kind);
        Assert.Empty(node.Values.Check(new RouteNpcValues { Npc = 4398 }));
        Assert.Empty(node.Values.Check(new RouteNpcValues { Npc = 4398, Replies = [36979, 36977] }));
        Assert.Equal(SettingProblemKind.OutOfRange, Assert.Single(node.Values.Check(new RouteNpcValues { Npc = -1 })).Kind);
    }

    [Fact]
    public void AnNpcActionIsWrittenAndReadBackWithItsRepliesInOrder()
    {
        var node = Node(RouteActions.Npc);
        var made = RouteActionCalls.Make(node, new RouteNpcValues { Npc = 4398, Replies = [36979, 36977] });
        var read = Assert.IsType<RouteNpcValues>(RouteActionCalls.Read(node, made).Settings);

        Assert.Equal(["Npc", "Replies"], made.Values.Keys);
        Assert.Equal(4398, read.Npc);
        Assert.Equal([36979, 36977], read.Replies);
    }

    [Fact]
    public void AForcedExitHoldsASideAndACellWhichIsPickedAmongTheMapsOwn()
    {
        var node = Node(RouteActions.Exit);

        Assert.Equal([SettingKind.Choice, SettingKind.Cell], node.Values.All.Select(setting => setting.Kind));
        Assert.Equal("Leave by {Direction}, or by cell {Cell}", node.Sentence.In(CultureInfo.InvariantCulture));
        Assert.Equal(["the top", "the right", "the bottom", "the left"], node.Values.All[0].Options.Select(option => option.Text.In(CultureInfo.InvariantCulture)));
        Assert.Equal((0, 559), (node.Values.All[1].Minimum, node.Values.All[1].Maximum));
    }

    [Fact]
    public void ACellOutsideTheMapIsOutOfRangeAndADoorNeedsOne()
    {
        var exit = Node(RouteActions.Exit).Values;
        var door = Node(RouteActions.Door).Values;
        var exitValues = new RouteExitValues { Cell = 560 };

        Assert.Equal(SettingProblemKind.OutOfRange, Assert.Single(exit.Check(exitValues)).Kind);
        Assert.Empty(exit.Check(new RouteExitValues { Cell = 559 }));
        Assert.Empty(exit.Check(new RouteExitValues()));
        Assert.Equal(SettingProblemKind.Required, Assert.Single(door.Check(new RouteDoorValues())).Kind);
        Assert.Empty(door.Check(new RouteDoorValues { Cell = 0 }));
    }

    [Fact]
    public void ACellIsWrittenAndReadBackAndAnUnreadableOneIsReported()
    {
        var exit = Node(RouteActions.Exit).Values;
        var written = exit.Write(new RouteExitValues { Direction = RouteDirection.Left, Cell = 321 });
        var read = Assert.IsType<RouteExitValues>(exit.Read(written).Settings);

        Assert.Equal((RouteDirection.Left, 321), (read.Direction, read.Cell));

        var wrong = exit.Read("""{"Cell":"here"}""");

        Assert.Equal(["Cell"], wrong.Reset.Select(setting => setting.Key));
        Assert.Null(Assert.IsType<RouteExitValues>(wrong.Settings).Cell);
    }

    [Fact]
    public void AnActionKeepsOnlyTheValuesThatDifferFromTheDefaultsAndReadsBackAsItWas()
    {
        var harvest = Node(RouteActions.Harvest);
        var made = RouteActionCalls.Make(harvest, new RouteHarvestValues { Resources = [303, 289] });

        Assert.Equal("route.harvest", made.Node);
        Assert.Equal(["Resources"], made.Values.Keys);
        Assert.Equal([303, 289], Assert.IsType<RouteHarvestValues>(RouteActionCalls.Read(harvest, made).Settings).Resources);

        var exit = Node(RouteActions.Exit);

        Assert.Empty(RouteActionCalls.Make(exit, new RouteExitValues()).Values);
        Assert.Equal("Left", RouteActionCalls.Make(exit, new RouteExitValues { Direction = RouteDirection.Left }).Values["Direction"]!.GetValue<string>());
        Assert.Empty(RouteActionCalls.Create(exit).Values);
        Assert.IsType<JsonArray>(made.Values["Resources"]);
    }

    [Fact]
    public void AFightFilterMustAskForAtLeastAsManyMonstersAsItAtMostAllows()
    {
        var fight = Node(RouteActions.Fight).Values;

        Assert.Empty(fight.Check(new RouteFightValues()));
        Assert.Single(fight.Check(new RouteFightValues { LeastMonsters = 5, MostMonsters = 3 }), problem => problem.Setting is null);
    }

    [Fact]
    public void AHarvestNeedsAtLeastOneResource()
    {
        Assert.Equal(SettingProblemKind.Required, Assert.Single(Node(RouteActions.Harvest).Values.Check(new RouteHarvestValues())).Kind);
    }

    [Fact]
    public void EveryValueOfAnActionIsNamedByItsSentenceSinceTheInspectorEditsThemAsTokensOfIt()
    {
        foreach (var id in RouteActions.Offered)
        {
            var node = Node(id);

            foreach (var language in new[] { "en", "fr" })
            {
                var sentence = node.Sentence.In(CultureInfo.GetCultureInfo(language));

                Assert.All(node.Values.All, setting => Assert.Contains("{" + setting.Key + "}", sentence, StringComparison.Ordinal));
            }
        }
    }
}
