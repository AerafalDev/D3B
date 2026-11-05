using System.ComponentModel;

namespace D3B.App.ViewModels.Studio;

/// <summary>An editor that takes back what was done and does it again: Edit's Undo and Redo, Ctrl Z and Ctrl Y.</summary>
internal interface IUndoable : INotifyPropertyChanged
{
    /// <summary>Gets whether something was done that Undo takes back.</summary>
    bool CanUndo { get; }

    /// <summary>Gets whether something was undone that Redo does again.</summary>
    bool CanRedo { get; }

    /// <summary>Takes back the last change.</summary>
    void Undo();

    /// <summary>Does again the change last taken back.</summary>
    void Redo();
}
