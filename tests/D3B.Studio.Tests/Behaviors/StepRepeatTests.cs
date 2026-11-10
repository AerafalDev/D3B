using D3B.Studio.Behaviors;
using static D3B.Studio.Tests.Behaviors.TestBehaviors;

namespace D3B.Studio.Tests.Behaviors;

public sealed class StepRepeatTests
{
    [Fact]
    public void OnceIsWhatAStepDoesWhenNothingIsSaid()
    {
        Assert.Equal(RepeatKind.Once, new NodeCall("wait", Values()).Repeat.Kind);
        Assert.Equal(RepeatKind.Once, StepRepeat.Once.Kind);
    }

    [Fact]
    public void ATimesRepeatCountsWholePassesAndOneIsOnce()
    {
        var three = StepRepeat.Times(3);

        Assert.Equal((RepeatKind.Times, 3), (three.Kind, three.Count));
        Assert.Same(StepRepeat.Once, StepRepeat.Times(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => StepRepeat.Times(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StepRepeat.Times(StepRepeat.MostTimes + 1));
    }

    [Fact]
    public void AnUntilRepeatKeepsItsConditionAndTwoRepeatsAreTheSameByWhatTheyHold()
    {
        var pods = new NodeCall("pods.atLeast", Values(("Percent", 90)));
        var again = new NodeCall("pods.atLeast", Values(("Percent", 90)));

        Assert.Same(pods, StepRepeat.Until(pods).Condition);
        Assert.True(StepRepeat.Until(pods).IsSame(StepRepeat.Until(again)));
        Assert.False(StepRepeat.Until(pods).IsSame(StepRepeat.Until(new NodeCall("pods.atLeast", Values(("Percent", 80))))));
        Assert.False(StepRepeat.Times(3).IsSame(StepRepeat.Times(4)));
        Assert.False(StepRepeat.Times(3).IsSame(StepRepeat.Forever));
        Assert.True(StepRepeat.Times(3).IsSame(StepRepeat.Times(3)));
    }
}
