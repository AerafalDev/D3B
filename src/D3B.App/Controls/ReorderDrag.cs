using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace D3B.App.Controls;

/// <summary>Lets the rows of a list be dragged to another place in it, or moved and removed from the keyboard (D376, D394).</summary>
/// <remarks>
/// Attached to an <see cref="ItemsControl"/> with a command, which is given a <see cref="ReorderRequest"/> once a row is dropped.
/// A row is picked up by a press that moves a few pixels, so a press that does not move is the click it was; a press on a field
/// typed in is left to it. A row that has the focus moves with Alt and the up and down arrows, and is removed with Delete when the
/// list has a remove command. The list is not moved here: the command changes the model, which builds its rows again.
/// </remarks>
internal static class ReorderDrag
{
    /// <summary>The command given a <see cref="ReorderRequest"/> when a row is dropped.</summary>
    public static readonly AttachedProperty<ICommand?> CommandProperty = AvaloniaProperty.RegisterAttached<ItemsControl, ICommand?>("Command", typeof(ReorderDrag));

    /// <summary>The command given the place of a row when Delete is pressed on it.</summary>
    public static readonly AttachedProperty<ICommand?> RemoveCommandProperty = AvaloniaProperty.RegisterAttached<ItemsControl, ICommand?>("RemoveCommand", typeof(ReorderDrag));

    private const double Threshold = 4;
    private const string Before = "DropBefore";
    private const string After = "DropAfter";

    private static readonly DataFormat<ReorderPayload> _row = DataFormat.CreateInProcessFormat<ReorderPayload>("d3b.reorder.row");

    // The press a drag may come of, kept on the list it happened in.
    private static readonly AttachedProperty<Press?> _pending = AvaloniaProperty.RegisterAttached<ItemsControl, Press?>("Pending", typeof(ReorderDrag));

    static ReorderDrag()
    {
        CommandProperty.Changed.AddClassHandler<ItemsControl>(OnCommandChanged);
    }

    /// <summary>Gets the command of a list.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The command, or <see langword="null"/>.</returns>
    public static ICommand? GetCommand(ItemsControl list)
    {
        ArgumentNullException.ThrowIfNull(list);

        return list.GetValue(CommandProperty);
    }

    /// <summary>Sets the command of a list.</summary>
    /// <param name="list">The list.</param>
    /// <param name="value">The command.</param>
    public static void SetCommand(ItemsControl list, ICommand? value)
    {
        ArgumentNullException.ThrowIfNull(list);

        list.SetValue(CommandProperty, value);
    }

    /// <summary>Gets the command that removes a row of a list.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The command, or <see langword="null"/>.</returns>
    public static ICommand? GetRemoveCommand(ItemsControl list)
    {
        ArgumentNullException.ThrowIfNull(list);

        return list.GetValue(RemoveCommandProperty);
    }

    /// <summary>Sets the command that removes a row of a list.</summary>
    /// <param name="list">The list.</param>
    /// <param name="value">The command, given the row's place.</param>
    public static void SetRemoveCommand(ItemsControl list, ICommand? value)
    {
        ArgumentNullException.ThrowIfNull(list);

        list.SetValue(RemoveCommandProperty, value);
    }

    /// <summary>Finds the place a row dropped at <paramref name="index"/>, before or after a row, takes once the dragged row left.</summary>
    /// <param name="from">The place of the row dragged.</param>
    /// <param name="index">The place of the row it is dropped on.</param>
    /// <param name="after">Whether it is dropped below the row, rather than above.</param>
    /// <returns>The place the row takes.</returns>
    internal static int Place(int from, int index, bool after)
    {
        var gap = after ? index + 1 : index;

        return from < gap ? gap - 1 : gap;
    }

    private static void OnCommandChanged(ItemsControl list, AvaloniaPropertyChangedEventArgs e)
    {
        list.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        list.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        list.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        list.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        list.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        list.RemoveHandler(DragDrop.DropEvent, OnDrop);
        list.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        list.ContainerPrepared -= OnContainerPrepared;

        if (e.NewValue is null)
            return;

        // A press on a button is handled by it: the list hears it all the same.
        list.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        list.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        list.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        list.AddHandler(DragDrop.DropEvent, OnDrop);
        // The list moves the focus by the arrows and handles them first, Alt or not.
        list.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        list.ContainerPrepared += OnContainerPrepared;
    }

    // A row takes the focus, to be moved and removed by the keys.
    private static void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        e.Container.Focusable = true;
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ItemsControl list || e.Source is not Visual source || IsTypedIn(source, list) || IndexOf(list, source) is not (>= 0 and var index))
            return;

        if (e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.Up or Key.Down)
        {
            var to = e.Key == Key.Up ? index - 1 : index + 1;

            if (to < 0 || to >= list.ItemCount || GetCommand(list) is not { } command)
                return;

            e.Handled = true;
            command.Execute(new ReorderRequest(index, to));

            // The model builds the rows again: the focus goes to the one that moved.
            Dispatcher.UIThread.Post(() => list.ContainerFromIndex(to)?.Focus(), DispatcherPriority.Background);
        }
        else if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Delete && GetRemoveCommand(list) is { } remove && remove.CanExecute(index))
        {
            e.Handled = true;
            remove.Execute(index);
            Dispatcher.UIThread.Post(() => list.ContainerFromIndex(Math.Min(index, list.ItemCount - 1))?.Focus(), DispatcherPriority.Background);
        }
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ItemsControl list || !e.GetCurrentPoint(list).Properties.IsLeftButtonPressed || e.Source is not Visual source || IsTypedIn(source, list))
            return;

        var index = IndexOf(list, source);

        if (index >= 0)
            list.SetValue(_pending, new Press(e, e.GetPosition(list), index));
    }

    private static async void OnMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not ItemsControl list || list.GetValue(_pending) is not { } press || !e.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
            return;

        var now = e.GetPosition(list);

        if (Math.Abs(now.X - press.At.X) < Threshold && Math.Abs(now.Y - press.At.Y) < Threshold)
            return;

        list.SetValue(_pending, null);

        try
        {
            var data = new DataTransfer();

            data.Add(DataTransferItem.Create(_row, new ReorderPayload(list, press.Index)));
            await DragDrop.DoDragDropAsync(press.Event, data, DragDropEffects.Move);
        }
        finally
        {
            Mark(list, -1, after: false);
        }
    }

    private static void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is ItemsControl list)
            list.SetValue(_pending, null);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not ItemsControl list || e.DataTransfer.TryGetValue(_row) is not { } payload || payload.List != list)
        {
            e.DragEffects = DragDropEffects.None;

            return;
        }

        var (index, after) = Target(list, e);

        Mark(list, index, after);
        e.DragEffects = index < 0 ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private static void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is ItemsControl list)
            Mark(list, -1, after: false);
    }

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not ItemsControl list || e.DataTransfer.TryGetValue(_row) is not { } payload || payload.List != list)
            return;

        var (index, after) = Target(list, e);

        Mark(list, -1, after: false);
        e.Handled = true;

        if (index < 0 || GetCommand(list) is not { } command)
            return;

        var request = new ReorderRequest(payload.Index, Place(payload.Index, index, after));

        if (request.From != request.To && command.CanExecute(request))
            command.Execute(request);
    }

    // The row under the pointer, and whether the pointer is in its lower half; past the last row it is the last, after.
    private static (int Index, bool After) Target(ItemsControl list, DragEventArgs e)
    {
        var count = list.ItemCount;

        if (count == 0)
            return (-1, false);

        for (var index = 0; index < count; index++)
        {
            if (list.ContainerFromIndex(index) is not { } container)
                continue;

            var at = e.GetPosition(container);

            if (at.Y < container.Bounds.Height || index == count - 1)
                return (index, at.Y >= container.Bounds.Height / 2);
        }

        return (count - 1, true);
    }

    private static void Mark(ItemsControl list, int index, bool after)
    {
        for (var at = 0; at < list.ItemCount; at++)
        {
            if (list.ContainerFromIndex(at) is not { } container)
                continue;

            container.Classes.Set(Before, at == index && !after);
            container.Classes.Set(After, at == index && after);
        }
    }

    private static int IndexOf(ItemsControl list, Visual source)
    {
        for (Visual? visual = source; visual is not null && visual != list; visual = visual.GetVisualParent())
        {
            if (visual is Control control && list.IndexFromContainer(control) is var index and >= 0)
                return index;
        }

        return -1;
    }

    // A field typed in keeps the press: dragging inside it selects its text.
    private static bool IsTypedIn(Visual source, ItemsControl list)
    {
        for (Visual? visual = source; visual is not null && visual != list; visual = visual.GetVisualParent())
        {
            if (visual is TextBox or ComboBox or Slider or NumericUpDown or ToggleSwitch or CheckBox)
                return true;
        }

        return false;
    }

    private sealed record ReorderPayload(ItemsControl List, int Index);

    private readonly record struct Press(PointerPressedEventArgs Event, Point At, int Index);
}
