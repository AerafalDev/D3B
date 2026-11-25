using System.Runtime.CompilerServices;

namespace D3B.App.Controls;

/// <summary>Keeps a field whose press closed its menu from opening it again with the same click, as a select does (D544).</summary>
/// <remarks>
/// A menu dismissed by a press outside closes on the press; the click of the field that opened it comes on the release, after it,
/// and would open it again. The click that follows such a close within a moment is that one: it opens nothing.
/// </remarks>
internal sealed class MenuReopenGuard
{
    private static readonly TimeSpan _sameClick = TimeSpan.FromMilliseconds(400);

    private readonly TimeProvider _time;
    private readonly ConditionalWeakTable<object, StrongBox<long>> _closed = [];

    /// <summary>Gets the guard of the app's fields.</summary>
    public static MenuReopenGuard Instance { get; } = new(TimeProvider.System);

    /// <summary>Initializes a new instance of the <see cref="MenuReopenGuard"/> class.</summary>
    /// <param name="time">The clock that says how long ago a menu closed.</param>
    public MenuReopenGuard(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Tells that a field's menu closed.</summary>
    /// <param name="field">The field.</param>
    /// <param name="byPointerOnField">Whether the pointer was on the field as it closed: a press on it closed it.</param>
    public void Closed(object field, bool byPointerOnField)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (byPointerOnField)
            _closed.AddOrUpdate(field, new StrongBox<long>(_time.GetTimestamp()));
    }

    /// <summary>Tells whether a click of the field may open its menu: not the click whose press just closed it.</summary>
    /// <param name="field">The field.</param>
    /// <returns>Whether the menu may open.</returns>
    public bool MayOpen(object field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (!_closed.TryGetValue(field, out var at))
            return true;

        _closed.Remove(field);

        return _time.GetElapsedTime(at.Value) >= _sameClick;
    }
}
