using System.Text.Json.Nodes;
using D3B.Bot.Assets;
using D3B.Bot.Behaviors;
using static D3B.Bot.Tests.Behaviors.TestBehaviors;

namespace D3B.Bot.Tests.Assets;

public sealed class AssetFilesTests
{
    private static BehaviorAsset Everything()
    {
        var graph = new ActionGraph(
            "if",
            [
                Node("if", NodeCatalog.Branch) with { Position = new NodePosition(10, 20) },
                Node("full", "pods.atLeast", ("Percent", Parameter("full"))),
                Note("yes", "full") with { IsDisabled = true },
                Node("set", NodeCatalog.SetVariable, ("variable", "count"), ("value", 2)),
            ],
            [
                new GraphLink("full", GraphPins.Value, "if", "Condition"),
                new GraphLink("if", GraphPins.True, "yes"),
                new GraphLink("if", GraphPins.False, "set"),
            ]);

        return new BehaviorAsset
        {
            Id = "b1",
            Name = "Everything",
            Parameters = [new BehaviorParameter("full", 90), new BehaviorParameter("area", null)],
            Variables = [new BehaviorVariable("count", VariableType.Number, VariableScope.Character, 1), new BehaviorVariable("seen", VariableType.Flag, VariableScope.Run)],
            Always = [new BehaviorState("safe", "Safe") { When = new AnyCondition([When("life.atLeast", ("Percent", 10)), new NotCondition(When("flag.on", ("First", Variable("seen"))))]), IsUrgent = true, Actions = Chain(Node("p", "pause")) }],
            Rules = [new BehaviorRule("r", "Lost", new NodeCall("fight.over", Values(("Result", "Lost")))) { When = new AllCondition([]), Actions = Chain(Note("n", "lost")) }],
            States = [new BehaviorState("work", "Work") { Repeats = true, States = [new BehaviorState("inner", "Inner") { Actions = graph }] }],
        };
    }

    [Fact]
    public void ABehaviorIsWrittenAndReadBackWhole()
    {
        var behavior = Everything();
        var json = AssetFiles.Write(behavior);
        var read = Assert.IsType<BehaviorAsset>(AssetFiles.Read(json, "Everything", "Everything.behavior.json", AssetKind.Behavior));

        // Written again, the file is the same: nothing was lost or added.
        Assert.Equal(json, AssetFiles.Write(read with { Path = null }));
        Assert.Equal(("b1", "Everything", "Everything.behavior.json"), (read.Id, read.Name, read.Path));
        Assert.Equal(new NodePosition(10, 20), read.States[0].States[0].Actions.Nodes[0].Position);
        Assert.True(read.States[0].States[0].Actions.Nodes[2].IsDisabled);
        Assert.Null(read.Parameters[1].Value);
        Assert.Contains("\"layout\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AProfileKeepsOnlyWhatItChangesAndANullIsAValue()
    {
        var profile = new ProfileAsset { Id = "p1", Name = "Mine", Behavior = "d3b.harvest", Values = Values(("Mode", "List"), ("Area", null)), Client = "3.7.12.12" };
        var read = Assert.IsType<ProfileAsset>(AssetFiles.Read(AssetFiles.Write(profile), "Mine", "Mine.profile.json", AssetKind.Profile));

        Assert.Equal(("d3b.harvest", "3.7.12.12"), (read.Behavior, read.Client));
        Assert.Equal(["Area", "Mode"], read.Values.Keys);
        Assert.Null(read.Values["Area"]);
        Assert.Equal("List", read.Values["Mode"]!.GetValue<string>());
    }

    [Fact]
    public void AnAssetOfAKindALaterSessionMakesKeepsWhatItsFileHolds()
    {
        const string json = """{"id":"z1","kind":"zone","format":1,"client":"3.7.12.12","maps":[1,2],"name":"ignored"}""";
        var read = Assert.IsType<PlannedAsset>(AssetFiles.Read(json, "North", "North.zone.json", AssetKind.Zone));

        Assert.Equal(AssetKind.Zone, read.Kind);
        Assert.Equal("[1,2]", read.Content["maps"]!.ToJsonString());
        Assert.Equal(json, JsonNode.Parse(AssetFiles.Write(read))!.ToJsonString());
    }

    [Theory]
    [InlineData("""{"id":"x","kind":"behavior","format":2}""", "The file's format is 2")]
    [InlineData("""{"id":"x","kind":"profile","format":1,"behavior":"b"}""", "The file says it is a profile, its extension a behavior.")]
    [InlineData("""{"kind":"behavior","format":1}""", "\"id\" is missing")]
    [InlineData("""not json""", "The file is no JSON")]
    public void AFileThatDoesNotReadSaysWhy(string json, string why)
    {
        var exception = Assert.Throws<AssetFormatException>(() => AssetFiles.Read(json, "x", "x.behavior.json", AssetKind.Behavior));

        Assert.StartsWith(why, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Harvest.behavior.json", AssetKind.Behavior, "Harvest")]
    [InlineData("Ash.Profile.JSON", AssetKind.Profile, "Ash")]
    [InlineData("Larvae.fight.json", AssetKind.FightPreferences, "Larvae")]
    public void AFilesExtensionSaysItsKind(string file, AssetKind kind, string name)
    {
        Assert.True(AssetFiles.TryKindOf(file, out var read, out var named));
        Assert.Equal((kind, name), (read, named));
    }

    [Fact]
    public void AFileOfNoKindIsNoAsset()
    {
        Assert.False(AssetFiles.TryKindOf("notes.json", out _, out _));
        Assert.False(AssetFiles.TryKindOf(".behavior.json", out _, out _));
    }
}
