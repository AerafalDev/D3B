using System.Collections.Immutable;
using System.Text.Json.Nodes;
using CsCheck;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.Studio.Tests.Routes;

public sealed class RouteClipsTests : IAsyncDisposable
{
    private static readonly NodeCall _ash = Call(RouteActions.Harvest, ("Resources", 1));
    private static readonly NodeCall _oak = Call(RouteActions.Harvest, ("Resources", 2));
    private static readonly NodeCall _fight = Call(RouteActions.Fight);
    private static readonly NodeCall _door = Call(RouteActions.Door, ("Cell", 312));

    private readonly Behaviors.TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    private static NodeCall Call(string node, params (string Key, int Value)[] values)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, JsonNode?>(StringComparer.Ordinal);

        foreach (var (key, value) in values)
            builder[key] = JsonValue.Create(value);

        return new NodeCall(node, builder.ToImmutable());
    }

    private static RouteAnchor Step(long map, params NodeCall[] actions)
    {
        return new RouteAnchor { MapId = map, Actions = [.. actions] };
    }

    private static ImmutableArray<RouteAnchor> Route(params RouteAnchor[] steps)
    {
        return [.. steps];
    }

    private static string[] Nodes(RouteAnchor anchor)
    {
        return [.. anchor.Actions.Select(action => action.Node)];
    }

    [Fact]
    public void EqualActionsOnConsecutiveStepsAreOneClipAcrossThem()
    {
        var anchors = Route(Step(1, _ash), Step(2, _ash, _fight), Step(3, _ash), Step(4));

        var clips = RouteClips.Derive(anchors);

        Assert.Equal(2, clips.Length);
        Assert.Equal((RouteActions.Harvest, 0, 2), (clips[0].Call.Node, clips[0].First, clips[0].Last));
        Assert.Equal((RouteActions.Fight, 1, 1), (clips[1].Call.Node, clips[1].First, clips[1].Last));
    }

    [Fact]
    public void ARouteWithNoActionHasNoClip()
    {
        Assert.Empty(RouteClips.Derive(Route(Step(1), Step(2))));
        Assert.Empty(RouteClips.Derive([]));
    }

    [Fact]
    public void AStepWithoutTheActionSplitsItsClipInTwo()
    {
        var clips = RouteClips.Derive(Route(Step(1, _ash), Step(2), Step(3, _ash), Step(4, _ash)));

        Assert.Equal([(0, 0), (2, 3)], clips.Select(clip => (clip.First, clip.Last)));
    }

    [Fact]
    public void ADifferentValueEndsAClipAndStartsAnotherOnTheSameTrack()
    {
        var clips = RouteClips.Derive(Route(Step(1, _ash), Step(2, _ash), Step(3, _oak), Step(4, _oak)));

        Assert.Equal([(0, 1, 0), (2, 3, 0)], clips.Select(clip => (clip.First, clip.Last, clip.Lane)));
        Assert.Equal([1, 2], clips.Select(clip => (int)clip.Call.Values["Resources"]!));
    }

    [Fact]
    public void ClipsOfOneKindThatOverlapStackInLanes()
    {
        var clips = RouteClips.Derive(Route(Step(1, _ash), Step(2, _ash, _oak), Step(3, _oak), Step(4, _ash)));

        Assert.Equal([(0, 1, 0), (1, 2, 1), (3, 3, 0)], clips.Select(clip => (clip.First, clip.Last, clip.Lane)));
    }

    [Fact]
    public void TwoEqualActionsOnOneStepAreTwoClipsAndNeverOne()
    {
        var clips = RouteClips.Derive(Route(Step(1, _ash), Step(2, _ash, _ash), Step(3, _ash)));

        Assert.Equal([(0, 2, 0, 0), (1, 1, 1, 1)], clips.Select(clip => (clip.First, clip.Last, clip.Layer, clip.Lane)));
    }

    [Fact]
    public void ClipsComeInTheOrderOfTheEditorsMenuThenAnUnknownNodeThenByTheirFirstStep()
    {
        var unknown = Call("mod.sing");
        var clips = RouteClips.Derive(Route(Step(1, unknown, _door), Step(2, _fight, _ash)));

        Assert.Equal([RouteActions.Harvest, RouteActions.Fight, RouteActions.Door, "mod.sing"], clips.Select(clip => clip.Call.Node));
    }

    [Fact]
    public void EveryActionTheEditorOffersAndAnActionTheHostDoesNotKnowMakeAClipAcrossTheStepsThatHoldThem()
    {
        var values = new Dictionary<string, object>
        {
            [RouteActions.Harvest] = new RouteHarvestValues { Resources = [1, 2] },
            [RouteActions.Fight] = new RouteFightValues { Monsters = [7], LeastMonsters = 2 },
            [RouteActions.Door] = new RouteDoorValues { Cell = 312 },
            [RouteActions.Exit] = new RouteExitValues { Direction = RouteDirection.Top, Cell = 300 },
            [RouteActions.Npc] = new RouteNpcValues { Npc = 4398, Replies = [36979, 36977] },
            [RouteActions.Follow] = new RouteFollowValues { Route = "another" },
        };
        var calls = new List<NodeCall> { Call("mod.sing", ("Volume", 3)) };

        foreach (var id in RouteActions.Offered.Concat([RouteActions.Follow]))
        {
            Assert.True(_behaviors.Nodes.TryGet(id, out var definition));
            calls.Add(RouteActionCalls.Create(definition));

            if (values.TryGetValue(id, out var made))
                calls.Add(RouteActionCalls.Make(definition, made));
        }

        Assert.Equal(RouteActions.Offered.Length + 1 + values.Count + 1, calls.Count);

        foreach (var call in calls)
        {
            var anchors = Route(Step(1), Step(2, call), Step(3, call), Step(4));
            var clip = Assert.Single(RouteClips.Derive(anchors));

            Assert.Equal((call.Node, 1, 2, 0), (clip.Call.Node, clip.First, clip.Last, clip.Layer));
            Assert.True(RouteAnchor.SameCall(call, clip.Call));

            var drawn = RouteClips.Draw(anchors, call, 0, 3);
            var whole = Assert.Single(RouteClips.Derive(drawn));

            Assert.Equal((0, 3), (whole.First, whole.Last));
            Assert.Equal(anchors, RouteClips.Reshape(drawn, whole, 1, 2));
            Assert.All(RouteClips.Remove(drawn, whole), anchor => Assert.Empty(anchor.Actions));
        }
    }

    [Fact]
    public void DrawingAppendsTheActionToEachStepAndLeavesTheOtherActionsAndWhatTheFileHoldsAsTheyWere()
    {
        var kept = new JsonObject { ["note"] = "kept" };
        var anchors = Route(Step(1, _door), new RouteAnchor { MapId = 2, IsPinned = true, Details = kept, Actions = [_fight] }, Step(3), Step(4, _door));

        var drawn = RouteClips.Draw(anchors, _ash, 0, 2);

        Assert.Equal([RouteActions.Door, RouteActions.Harvest], Nodes(drawn[0]));
        Assert.Equal([RouteActions.Fight, RouteActions.Harvest], Nodes(drawn[1]));
        Assert.Equal([RouteActions.Harvest], Nodes(drawn[2]));
        Assert.Same(anchors[3], drawn[3]);
        Assert.True(drawn[1].IsPinned);
        Assert.Equal(2, drawn[1].MapId);
        Assert.Same(kept, drawn[1].Details);
    }

    [Fact]
    public void DrawingSkipsTheStepsThatAlreadyHoldTheAction()
    {
        var anchors = Route(Step(1, _ash), Step(2), Step(3, _fight, _ash));

        var drawn = RouteClips.Draw(anchors, _ash, 0, 2);

        Assert.Same(anchors[0], drawn[0]);
        Assert.Same(anchors[2], drawn[2]);
        Assert.Equal([RouteActions.Harvest], Nodes(drawn[1]));
        Assert.Equal([(0, 2)], RouteClips.Derive(drawn).Where(clip => clip.Call.Node == RouteActions.Harvest).Select(clip => (clip.First, clip.Last)));
    }

    [Fact]
    public void DrawingOutsideTheRouteOrBackwardsIsRefused()
    {
        var anchors = Route(Step(1), Step(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => RouteClips.Draw(anchors, _ash, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RouteClips.Draw(anchors, _ash, 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => RouteClips.Draw(anchors, _ash, 1, 0));
    }

    [Fact]
    public void DraggingAnEdgeAddsTheActionOnTheStepsItPassesOverAndTakesItOffThoseItLeaves()
    {
        var anchors = Route(Step(1), Step(2, _ash), Step(3, _ash), Step(4, _ash), Step(5));
        var clip = Assert.Single(RouteClips.Derive(anchors));

        var longer = RouteClips.Reshape(anchors, clip, 0, 4);
        var shorter = RouteClips.Reshape(anchors, clip, 2, 3);

        Assert.Equal([(0, 4)], RouteClips.Derive(longer).Select(c => (c.First, c.Last)));
        Assert.Equal([(2, 3)], RouteClips.Derive(shorter).Select(c => (c.First, c.Last)));
        Assert.Empty(shorter[1].Actions);
    }

    [Fact]
    public void MovingAClipTakesItOffItsStepsAndAppendsItToTheNewOnesWithoutReorderingTheOthers()
    {
        var anchors = Route(Step(1, _ash, _fight), Step(2, _ash), Step(3, _door, _fight), Step(4, _door));
        var clip = RouteClips.Derive(anchors).Single(c => c.Call.Node == RouteActions.Harvest);

        var moved = RouteClips.Reshape(anchors, clip, 2, 3);

        Assert.Equal([RouteActions.Fight], Nodes(moved[0]));
        Assert.Empty(moved[1].Actions);
        Assert.Equal([RouteActions.Door, RouteActions.Fight, RouteActions.Harvest], Nodes(moved[2]));
        Assert.Equal([RouteActions.Door, RouteActions.Harvest], Nodes(moved[3]));
    }

    [Fact]
    public void RemovingAClipTakesOnlyItsOwnActionOffTheStepsItCovers()
    {
        var anchors = Route(Step(1, _ash, _fight), Step(2, _ash, _oak), Step(3, _fight));
        var clip = RouteClips.Derive(anchors).Single(c => RouteAnchor.SameCall(c.Call, _ash));

        var removed = RouteClips.Remove(anchors, clip);

        Assert.Equal([RouteActions.Fight], Nodes(removed[0]));
        Assert.Equal([_oak], removed[1].Actions);
        Assert.Same(anchors[2], removed[2]);
    }

    [Fact]
    public void RemovingTheSecondOfTwoEqualActionsLeavesTheFirst()
    {
        var anchors = Route(Step(1, _ash, _ash), Step(2, _ash));
        var second = RouteClips.Derive(anchors).Single(clip => clip.Layer == 1);

        var removed = RouteClips.Remove(anchors, second);

        Assert.Equal([_ash], removed[0].Actions);
        Assert.Equal([_ash], removed[1].Actions);
    }

    [Fact]
    public void ChangingTheValuesOfAClipChangesThemInPlaceOnEveryStepItCoversAndKeepsTheOrder()
    {
        var anchors = Route(Step(1, _ash, _fight), Step(2, _fight, _ash), Step(3, _ash));
        var clip = RouteClips.Derive(anchors).Single(c => c.Call.Node == RouteActions.Harvest);

        var changed = RouteClips.Revalue(anchors, clip, _oak);

        Assert.Equal([_oak, _fight], changed[0].Actions);
        Assert.Equal([_fight, _oak], changed[1].Actions);
        Assert.Equal([_oak], changed[2].Actions);
    }

    [Fact]
    public void AValueChangedOnOneStepSplitsTheClipAroundIt()
    {
        var anchors = Route(Step(1, _ash), Step(2, _oak), Step(3, _ash));

        var clips = RouteClips.Derive(anchors);

        Assert.Equal([(0, 0, 0), (1, 1, 0), (2, 2, 0)], clips.Select(clip => (clip.First, clip.Last, clip.Lane)));
    }

    [Fact]
    public void EveryActionOfARouteBelongsToExactlyOneClipWhateverTheRoute()
    {
        Gen.Int[0, 3].Array[0, 3].Array[1, 7].Sample(routes =>
        {
            var pool = new[] { _ash, _oak, _fight, _door };
            var anchors = Route([.. routes.Select((picks, index) => Step(index, [.. picks.Select(pick => pool[pick])]))]);
            var clips = RouteClips.Derive(anchors);

            Assert.Equal(anchors.Sum(anchor => anchor.Actions.Length), clips.Sum(clip => clip.Last - clip.First + 1));

            foreach (var clip in clips)
            {
                for (var step = clip.First; step <= clip.Last; step++)
                    Assert.Contains(anchors[step].Actions, action => RouteAnchor.SameCall(action, clip.Call));
            }
        });
    }

    [Fact]
    public void RemovingClipsOneByOneEmptiesAnyRouteAndNeverTouchesWhatIsNotAnAction()
    {
        Gen.Int[0, 3].Array[0, 3].Array[1, 7].Sample(routes =>
        {
            var pool = new[] { _ash, _oak, _fight, _door };
            var anchors = Route([.. routes.Select((picks, index) => new RouteAnchor { MapId = index, IsPinned = index % 2 == 0, Actions = [.. picks.Select(pick => pool[pick])] })]);

            while (RouteClips.Derive(anchors) is { Length: > 0 } clips)
            {
                var before = anchors.Sum(anchor => anchor.Actions.Length);

                anchors = RouteClips.Remove(anchors, clips[0]);

                Assert.True(anchors.Sum(anchor => anchor.Actions.Length) < before);
            }

            Assert.Equal(routes.Length, anchors.Length);
            Assert.All(anchors.Select((anchor, index) => (anchor, index)), pair => Assert.Equal((pair.index, pair.index % 2 == 0), (pair.anchor.MapId, pair.anchor.IsPinned)));
        });
    }

    [Fact]
    public void DrawingKeepsEveryActionTheStepsHeldAndPutsTheNewOneLast()
    {
        Gen.Int[0, 3].Array[0, 3].Array[1, 7].Sample(routes =>
        {
            var pool = new[] { _ash, _oak, _fight, _door };
            var anchors = Route([.. routes.Select((picks, index) => Step(index, [.. picks.Select(pick => pool[pick])]))]);
            var last = anchors.Length - 1;

            var drawn = RouteClips.Draw(anchors, _door, 0, last);

            for (var step = 0; step <= last; step++)
            {
                Assert.StartsWith(string.Join(',', anchors[step].Actions.Select(Describe)), string.Join(',', drawn[step].Actions.Select(Describe)), StringComparison.Ordinal);
                Assert.Contains(drawn[step].Actions, action => RouteAnchor.SameCall(action, _door));
            }
        });
    }

    [Fact]
    public void ActionsHeldByEverySelectedStepAreTheOnesTheyHaveInCommonInTheOrderOfTheFirst()
    {
        var anchors = Route(Step(1, _fight, _ash), Step(2, _ash, _fight, _door), Step(3, _oak, _fight), Step(4, _ash));

        Assert.Equal([_fight, _ash], RouteClips.CommonCalls(anchors, [0, 1]), new SameCalls());
        Assert.Equal([_fight], RouteClips.CommonCalls(anchors, [0, 1, 2]), new SameCalls());
        Assert.Empty(RouteClips.CommonCalls(anchors, [0, 3, 2]));
        Assert.Empty(RouteClips.CommonCalls(anchors, []));
    }

    [Fact]
    public void ChangingAnActionOnSomeStepsLeavesTheOthersAndKeepsTheOrder()
    {
        var anchors = Route(Step(1, _fight, _ash), Step(2, _ash), Step(3, _ash, _fight), Step(4, _door));

        var changed = RouteClips.RevalueOn(anchors, [0, 2, 3], _ash, _oak);

        Assert.Equal([_fight, _oak], changed[0].Actions);
        Assert.Equal([_ash], changed[1].Actions);
        Assert.Equal([_oak, _fight], changed[2].Actions);
        Assert.Same(anchors[3], changed[3]);
    }

    private sealed class SameCalls : IEqualityComparer<NodeCall>
    {
        public bool Equals(NodeCall? x, NodeCall? y)
        {
            return RouteAnchor.SameCall(x, y);
        }

        public int GetHashCode(NodeCall call)
        {
            return call.Node.GetHashCode(StringComparison.Ordinal);
        }
    }

    [Fact]
    public void StretchingTheSecondOfTwoEqualActionsOverAStepThatHoldsNoneGivesThatStepTwoSoTheClipKeepsItsLayer()
    {
        var anchors = Route(Step(1, _ash, _ash), Step(2), Step(3));
        var second = RouteClips.Derive(anchors).Single(clip => clip.Layer == 1);

        var stretched = RouteClips.Reshape(anchors, second, 0, 1);
        var clips = RouteClips.Derive(stretched);

        Assert.Equal([_ash, _ash], stretched[1].Actions, new SameCalls());
        Assert.Equal([(0, 1, 0), (0, 1, 1)], clips.Select(clip => (clip.First, clip.Last, clip.Layer)));
    }

    private static string Describe(NodeCall call)
    {
        return call.Node + string.Concat(call.Values.Select(pair => $"|{pair.Key}={pair.Value}"));
    }
}
