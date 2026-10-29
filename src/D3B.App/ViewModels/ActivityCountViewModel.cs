namespace D3B.App.ViewModels;

/// <summary>Something an activity counts, as a bot's card shows it: its text, and whether an item's icon goes beside it (D131).</summary>
/// <param name="Text">What it says, such as "Ash 142".</param>
/// <param name="IsItem">Whether it counts an item, whose icon goes beside it once the pack holds icons.</param>
internal sealed record ActivityCountViewModel(string Text, bool IsItem);
