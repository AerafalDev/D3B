using System.Collections.ObjectModel;
using D3B.App.ViewModels.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>The actions of the step chosen, as rows with the form of each one's settings, and what removing, moving or editing one does to the route (D377).</summary>
/// <remarks>The anchors are the editor's: the list changes the step chosen in them and tells the editor, which records the change, once it did.</remarks>
internal sealed class RouteActionList
{
    private readonly RoutesViewModel _page;
    private readonly List<RouteAnchor> _anchors;
    private readonly Func<int> _selected;
    private readonly Action _changed;
    private readonly Action _built;

    /// <summary>Initializes a new instance of the <see cref="RouteActionList"/> class.</summary>
    /// <param name="page">The routes, which hold what the forms read.</param>
    /// <param name="anchors">The route's anchors, which the editor holds and the list changes.</param>
    /// <param name="selected">Gives the step chosen, or -1.</param>
    /// <param name="changed">Called once an anchor was changed.</param>
    /// <param name="built">Called once the rows were made again, for the editor to say what follows from them.</param>
    public RouteActionList(RoutesViewModel page, List<RouteAnchor> anchors, Func<int> selected, Action changed, Action built)
    {
        _page = page;
        _anchors = anchors;
        _selected = selected;
        _changed = changed;
        _built = built;
    }

    /// <summary>Gets the rows, in the step's order.</summary>
    public ObservableCollection<RouteActionViewModel> Rows { get; } = [];

    /// <summary>Gets whether the forms stay as they are while the route changes: rebuilding them under the hand that types in one would take its focus.</summary>
    public bool KeepsForms { get; private set; }

    /// <summary>Makes the rows of the step chosen again.</summary>
    public void Build()
    {
        Rows.Clear();

        if (HasSelectedStep)
        {
            foreach (var row in RouteActionForms.Build(_page.Services!, _page.Localizer, _page.Dialogs, _anchors[_selected()], Edited, Remove, Move))
                Rows.Add(row);
        }

        _built();
    }

    /// <summary>Removes an action of the step chosen.</summary>
    /// <param name="index">The action's place in the step.</param>
    public void Remove(int index)
    {
        var step = _selected();

        if (!HasSelectedStep || index < 0 || index >= _anchors[step].Actions.Length)
            return;

        _anchors[step] = _anchors[step] with { Actions = _anchors[step].Actions.RemoveAt(index) };
        _changed();
    }

    /// <summary>Moves an action of the step chosen.</summary>
    /// <param name="index">The action's place in the step.</param>
    /// <param name="delta">How many places towards the end, negative towards the start.</param>
    public void Move(int index, int delta)
    {
        var step = _selected();
        var to = index + delta;

        if (!HasSelectedStep || index < 0 || to < 0 || index >= _anchors[step].Actions.Length || to >= _anchors[step].Actions.Length)
            return;

        var actions = _anchors[step].Actions;
        var moved = actions[index];

        _anchors[step] = _anchors[step] with { Actions = actions.RemoveAt(index).Insert(to, moved) };
        _changed();
    }

    private bool HasSelectedStep
    {
        get
        {
            var step = _selected();

            return step >= 0 && step < _anchors.Count;
        }
    }

    private void Edited(int index, NodeDefinition definition, SettingsEditor editor)
    {
        var step = _selected();

        if (!HasSelectedStep || index >= _anchors[step].Actions.Length || index >= Rows.Count)
            return;

        var anchor = _anchors[step];
        var made = RouteActionCalls.Make(definition, editor.Draft);

        if (!RouteAnchor.SameCall(anchor.Actions[index], made))
        {
            _anchors[step] = anchor with { Actions = anchor.Actions.SetItem(index, made) };

            // The forms stay as they are: rebuilding them under the hand that types in one would take its focus.
            KeepsForms = true;

            try
            {
                _changed();
            }
            finally
            {
                KeepsForms = false;
            }
        }

        Rows[index].Say();
    }
}
