using System.Text.Json.Nodes;
using D3B.Bot.Behaviors;
using static D3B.Bot.Tests.Behaviors.TestBehaviors;

namespace D3B.Bot.Tests.Behaviors;

public sealed class BehaviorCompilerTests : IAsyncDisposable
{
    private readonly TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    private IReadOnlyList<(BehaviorProblemKind, string)> Problems(D3B.Bot.Assets.BehaviorAsset behavior, params (string Key, JsonNode? Value)[] profile)
    {
        var compilation = _behaviors.Compile(behavior, profile);

        Assert.Equal(compilation.Problems.Count == 0, compilation.Holds);

        return [.. compilation.Problems.Select(problem => (problem.Kind, problem.Where))];
    }

    [Fact]
    public void ABehaviorThatHoldsCompiles()
    {
        var behavior = Behavior(
            new BehaviorState("full", "Full") { When = new AllCondition([When("pods.atLeast", ("Percent", 90)), new NotCondition(When("life.atLeast", ("Percent", 10)))]), Actions = Chain(Note("n", "full")) },
            new BehaviorState("work", "Work") { Actions = Chain(Node("act", SettingsActivityId, ("Walks", 3))) });

        Assert.Empty(Problems(behavior));
    }

    [Fact]
    public void UnknownNodesAndValuesAreSaidWhereTheyAre()
    {
        var behavior = Behavior(new BehaviorState("s", "S")
        {
            When = When("nothing.such"),
            Actions = Chain(Node("a", "wait", ("Hours", 1)), Node("b", "wait", ("Seconds", "many"))),
        });

        Assert.Equal(
            [(BehaviorProblemKind.NodeUnknown, "s"), (BehaviorProblemKind.ValueUnknown, "s/a"), (BehaviorProblemKind.ValueDoesNotRead, "s/b")],
            Problems(behavior));
    }

    [Fact]
    public void ANodeWhereItsKindDoesNotGoIsMisplaced()
    {
        var behavior = Behavior(new BehaviorState("s", "S") { When = When("wait"), Actions = Chain(Node("e", "map.changed")) });

        Assert.Equal([(BehaviorProblemKind.NodeMisplaced, "s"), (BehaviorProblemKind.NodeMisplaced, "s/e"), (BehaviorProblemKind.LinkBroken, "s")], Problems(behavior));
    }

    [Fact]
    public void ValuesOutOfTheirBoundsDoNotHold()
    {
        var behavior = Behavior(new BehaviorState("s", "S") { Actions = Chain(Node("w", "wait", ("Seconds", 0))) });

        Assert.Equal([(BehaviorProblemKind.ValueInvalid, "s/w")], Problems(behavior));
    }

    [Fact]
    public void PublicSettingsAndVariablesMustBeDeclaredAndOfTheInputsType()
    {
        var behavior = Behavior(new BehaviorState("s", "S")
        {
            Actions = Chain(
                Node("a", "wait", ("Seconds", Parameter("missing"))),
                Node("b", "wait", ("Seconds", Variable("missing"))),
                Node("c", "wait", ("Seconds", Variable("text")))),
        }) with
        {
            Variables = [new BehaviorVariable("text", VariableType.Text, VariableScope.Run)],
        };

        Assert.Equal(
            [(BehaviorProblemKind.ParameterUnknown, "s/a"), (BehaviorProblemKind.VariableUnknown, "s/b"), (BehaviorProblemKind.TypesDiffer, "s/c")],
            Problems(behavior));
    }

    [Fact]
    public void AValueTheProfileChangesThatDoesNotReadStopsIt()
    {
        var behavior = Behavior(new BehaviorState("s", "S") { Actions = Chain(Node("w", "wait", ("Seconds", Parameter("seconds")))) }) with
        {
            Parameters = [new BehaviorParameter("seconds", 5)],
        };

        Assert.Empty(Problems(behavior, ("seconds", 30), ("gone", 1)));
        Assert.Equal([(BehaviorProblemKind.ProfileValueDoesNotRead, "seconds")], Problems(behavior, ("seconds", "soon")));
    }

    [Fact]
    public void AWireBringsAValueOfItsInputsTypeOrIsRefused()
    {
        var graph = new ActionGraph(
            "w",
            [Node("w", "wait"), Node("c", "pods.atLeast")],
            [new GraphLink("c", GraphPins.Value, "w", "Seconds")]);

        Assert.Equal([(BehaviorProblemKind.TypesDiffer, "s/w")], Problems(Behavior(new BehaviorState("s", "S") { Actions = graph })));
    }

    [Fact]
    public void AWayOfExecutionThatComesBackOnItselfIsALoop()
    {
        var graph = new ActionGraph(
            "a",
            [Note("a", "a"), Note("b", "b")],
            [new GraphLink("a", GraphPins.Next, "b"), new GraphLink("b", GraphPins.Next, "a")]);

        Assert.Equal([(BehaviorProblemKind.Loop, "s/a")], Problems(Behavior(new BehaviorState("s", "S") { Actions = graph })));
    }

    [Fact]
    public void ADisabledNodeIsPassedByTheWay()
    {
        var graph = Chain(Note("a", "a"), Node("w", "wait", ("Seconds", 0)) with { IsDisabled = true }, Note("b", "b"));
        var compilation = _behaviors.Compile(Behavior(new BehaviorState("s", "S") { Actions = graph }));

        // Disabled, its values are still checked: enabling it again must not surprise.
        Assert.Equal([BehaviorProblemKind.ValueInvalid], compilation.Problems.Select(problem => problem.Kind));
    }

    [Fact]
    public void ARulePlaysOnlyActionsPlayedAtOnce()
    {
        var behavior = Behavior(new BehaviorState("s", "S")) with
        {
            Rules = [new BehaviorRule("r", "R", new NodeCall("fight.over", Values(("Result", "Lost")))) { Actions = Chain(Note("n", "lost"), Node("w", "wait")) }],
        };

        Assert.Equal([(BehaviorProblemKind.NotInstant, "r/w")], Problems(behavior));
    }

    [Fact]
    public void IdsAreUniqueInABehaviorAndAStateHoldsStatesOrActions()
    {
        var behavior = Behavior(
            new BehaviorState("s", "S") { States = [new BehaviorState("t", "T")], Actions = Chain(Note("t", "t")) });

        Assert.Equal([(BehaviorProblemKind.StateMixed, "s"), (BehaviorProblemKind.IdRepeated, "s/t"), (BehaviorProblemKind.LinkBroken, "s")], Problems(behavior));
    }
}
