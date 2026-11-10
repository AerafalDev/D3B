using Avalonia.Controls;

namespace D3B.App.Views;

/// <summary>A node's sentence with its values as tokens that edit them where they stand: an action of a step, a step of a list, the condition of an "until" (D380, D421).</summary>
internal sealed partial class ActionSentenceView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="ActionSentenceView"/> class.</summary>
    public ActionSentenceView()
    {
        InitializeComponent();
    }
}
