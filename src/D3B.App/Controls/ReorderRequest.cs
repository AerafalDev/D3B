namespace D3B.App.Controls;

/// <summary>A row dragged to another place in its list (D376, D394).</summary>
/// <param name="From">The row's place before, from 0.</param>
/// <param name="To">The place it takes once it left its own, from 0.</param>
internal readonly record struct ReorderRequest(int From, int To);
