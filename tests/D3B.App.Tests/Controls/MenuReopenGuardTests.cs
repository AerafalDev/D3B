using D3B.App.Controls;
using Microsoft.Extensions.Time.Testing;

namespace D3B.App.Tests.Controls;

public sealed class MenuReopenGuardTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly object _field = new();

    [Fact]
    public void TheClickThatClosedTheMenuOnItsFieldDoesNotOpenItAgain()
    {
        var guard = new MenuReopenGuard(_time);

        Assert.True(guard.MayOpen(_field));

        // The press on the field closes the menu, then its click comes: as a select, the menu stays closed.
        guard.Closed(_field, byPointerOnField: true);
        _time.Advance(TimeSpan.FromMilliseconds(120));

        Assert.False(guard.MayOpen(_field));

        // The next click opens it again.
        Assert.True(guard.MayOpen(_field));
    }

    [Fact]
    public void AMenuClosedElsewhereOrLongBeforeOpensAtTheNextClick()
    {
        var guard = new MenuReopenGuard(_time);

        guard.Closed(_field, byPointerOnField: false);

        Assert.True(guard.MayOpen(_field));

        guard.Closed(_field, byPointerOnField: true);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.True(guard.MayOpen(_field));
        Assert.True(guard.MayOpen(new object()));
    }
}
