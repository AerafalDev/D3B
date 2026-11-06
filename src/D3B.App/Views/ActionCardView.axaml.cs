using Avalonia.Controls;

namespace D3B.App.Views;

/// <summary>An action of a step, or a step of a list: the node's sentence with its values as tokens, and what moves or removes it (D380, D386).</summary>
internal sealed partial class ActionCardView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="ActionCardView"/> class.</summary>
    public ActionCardView()
    {
        InitializeComponent();
    }
}
