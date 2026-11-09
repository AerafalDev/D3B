using D3B.App.ViewModels.Studio;

namespace D3B.App.Tests.ViewModels.Studio;

public sealed class UndoHistoryTests
{
    private static UndoHistory<string> History(int depth = 200)
    {
        return new UndoHistory<string>("a", string.Equals, depth);
    }

    [Fact]
    public void EachStateIsTakenBackInTurnAndDoneAgain()
    {
        var history = History();

        Assert.True(history.Record("b"));
        Assert.True(history.Record("c"));
        Assert.True(history.TryUndo(out var state));
        Assert.Equal("b", state);
        Assert.True(history.TryUndo(out state));
        Assert.Equal("a", state);
        Assert.False(history.TryUndo(out state));
        Assert.Equal("a", state);
        Assert.True(history.TryRedo(out state));
        Assert.Equal("b", state);
        Assert.True(history.TryRedo(out state));
        Assert.Equal("c", state);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ASameStateIsNotAChangeAndAChangeAfterAnUndoClearsWhatWasToDoAgain()
    {
        var history = History();

        Assert.False(history.Record("a"));

        history.Record("b");
        history.TryUndo(out _);

        Assert.True(history.CanRedo);

        history.Record("c");

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ChangesOfTheSameRunAreOneAndAnotherRunOrNoneBreaksIt()
    {
        var history = History();

        history.Record("b", "typing");
        history.Record("bc", "typing");
        history.Record("bcd", "typing");
        history.Record("x", "other");
        history.Record("y");
        history.Record("z");

        Assert.True(history.TryUndo(out var state));
        Assert.Equal("y", state);
        Assert.True(history.TryUndo(out state));
        Assert.Equal("x", state);
        Assert.True(history.TryUndo(out state));
        Assert.Equal("bcd", state);
        Assert.True(history.TryUndo(out state));
        Assert.Equal("a", state);
    }

    [Fact]
    public void AnUndoEndsARunSoTypingAfterItIsAChangeOfItsOwn()
    {
        var history = History();

        history.Record("b", "typing");
        history.Record("c", "typing");
        history.TryUndo(out _);
        history.Record("d", "typing");
        history.Record("e", "typing");

        Assert.True(history.TryUndo(out var state));
        Assert.Equal("a", state);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void TheOldestStateIsForgottenPastTheDepthAndResetStartsAgain()
    {
        var history = History(depth: 2);

        history.Record("b");
        history.Record("c");
        history.Record("d");
        history.TryUndo(out _);

        Assert.True(history.TryUndo(out var state));
        Assert.Equal("b", state);
        Assert.False(history.CanUndo);

        history.Reset("z");

        Assert.Equal("z", history.Current);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }
}
