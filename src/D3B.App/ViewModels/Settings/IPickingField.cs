using System.Windows.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>A field whose value is picked in a dialog of its own, so that its token in an action's sentence opens the dialog at once (D298, D380, D384).</summary>
internal interface IPickingField
{
    /// <summary>Gets what opens the dialog.</summary>
    ICommand PickCommand { get; }
}
