using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.App.ViewModels.Sequences;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;
using D3B.Studio.Routes;

namespace D3B.App.ViewModels.Routes;

/// <summary>An action of a step in the inspector: the node's sentence, its values tokens that edit them where they stand (D377, D380).</summary>
/// <remarks>
/// The sentence is the node's own, cut where it names a value: each is a token showing the value as chosen, and a press on it
/// opens the typed field, a cell the map's own cells. An action whose node the host does not know is kept as it is and said so.
/// </remarks>
internal sealed partial class RouteActionViewModel : ObservableObject
{
    private static readonly Regex _slot = SlotPattern();

    private readonly NodeDefinition? _definition;
    private readonly Localizer _localizer;
    private readonly Func<SettingKind, int, string> _name;
    private readonly Action<int> _remove;
    private readonly Action<int, int> _move;
    private readonly List<(ActionPartViewModel Part, string Key)> _tokens = [];

    /// <summary>Gets the action's place in the step, from 0.</summary>
    public int Index { get; }

    /// <summary>Gets the action's node id.</summary>
    public string NodeId { get; }

    /// <summary>Gets the editor of its values, or <see langword="null"/> for an action the host does not know.</summary>
    public SettingsEditor? Editor { get; }

    /// <summary>Gets the sentence cut into words and tokens.</summary>
    public IReadOnlyList<ActionPartViewModel> Parts { get; }

    /// <summary>Gets how the step of a list is repeated; none for an action of a route, which is played once (D420).</summary>
    public StepRepeatViewModel? Repeat { get; init; }

    /// <summary>Gets whether the host does not know the action's node, which the file keeps as it is.</summary>
    public bool IsUnknown => _definition is null;

    /// <summary>Gets whether the run plays the action today (D377).</summary>
    public bool IsPlayed { get; }

    /// <summary>Gets the action's place counted from 1, as a list of steps numbers it.</summary>
    public int Number => Index + 1;

    /// <summary>Gets whether the action is the first of its step.</summary>
    public bool IsFirst => Index == 0;

    /// <summary>Gets whether the action is the last of its step.</summary>
    public bool IsLast { get; }

    /// <summary>Gets the sentence, its values filled in, as a screen reader says it.</summary>
    [ObservableProperty]
    public partial string Sentence { get; private set; } = "";

    /// <summary>Gets whether something is wrong with the values.</summary>
    [ObservableProperty]
    public partial bool HasProblems { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="RouteActionViewModel"/> class.</summary>
    /// <param name="index">The action's place in the step.</param>
    /// <param name="count">How many actions the step has.</param>
    /// <param name="nodeId">The action's node id.</param>
    /// <param name="definition">The node, or <see langword="null"/> when the host does not know it.</param>
    /// <param name="editor">The editor of its values.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="name">Names a value of the game.</param>
    /// <param name="remove">What removing does, given the place.</param>
    /// <param name="move">What moving does, given the place and how many places towards the end.</param>
    public RouteActionViewModel(int index, int count, string nodeId, NodeDefinition? definition, SettingsEditor? editor, Localizer localizer, Func<SettingKind, int, string> name, Action<int> remove, Action<int, int> move)
    {
        Index = index;
        IsLast = index == count - 1;
        NodeId = nodeId;
        _definition = definition;
        Editor = editor;
        _localizer = localizer;
        _name = name;
        _remove = remove;
        _move = move;
        IsPlayed = definition is not null && (RouteActions.IsPlayed(nodeId) || BehaviorSequences.Offered.Contains(nodeId) || definition.Role is NodeRole.Activity);
        Parts = Cut();
        Say();
    }

    /// <summary>Says the sentence and the problems again, once the values changed.</summary>
    public void Say()
    {
        if (_definition is null || Editor is null)
        {
            Sentence = _localizer.Format("RouteActionUnknown", NodeId);

            return;
        }

        foreach (var (part, key) in _tokens)
            part.Show(Value(key));

        Sentence = string.Concat(Parts.Select(part => part.Text));
        HasProblems = Editor.HasProblems;
    }

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex SlotPattern();

    [RelayCommand]
    private void Remove()
    {
        _remove(Index);
    }

    [RelayCommand]
    private void MoveUp()
    {
        _move(Index, -1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        _move(Index, 1);
    }

    private List<ActionPartViewModel> Cut()
    {
        if (_definition is null || Editor is null)
            return [new ActionPartViewModel(_localizer.Format("RouteActionUnknown", NodeId), null)];

        var text = _definition.Sentence.In(_localizer.Culture);
        var parts = new List<ActionPartViewModel>();
        var from = 0;

        foreach (Match match in _slot.Matches(text))
        {
            if (match.Index > from)
                parts.Add(new ActionPartViewModel(text[from..match.Index], null));

            var key = match.Groups[1].Value;
            var field = Editor.FieldOf(key);
            var part = new ActionPartViewModel(field is null ? match.Value : Value(key), field);

            parts.Add(part);

            if (field is not null)
                _tokens.Add((part, key));

            from = match.Index + match.Length;
        }

        if (from < text.Length)
            parts.Add(new ActionPartViewModel(text[from..], null));

        return parts;
    }

    private string Value(string key)
    {
        var setting = _definition!.Values.All.FirstOrDefault(candidate => candidate.Key == key);

        if (setting is null || Editor?.Draft is not { } draft)
            return "{" + key + "}";

        var culture = _localizer.Culture;

        switch (setting.Kind)
        {
            case SettingKind.Choice:
                return setting.Options.FirstOrDefault(option => Equals(option.Value, setting.GetValue(draft)))?.Text.In(culture) ?? "…";
            case SettingKind.Cell:
                return setting.GetValue(draft) is int cell ? cell.ToString(culture) : _localizer["CellNone"];
            case SettingKind.Number:
                return Convert.ToString(setting.GetValue(draft), culture) ?? "";
            case SettingKind.Route:
                return setting.GetValue(draft) is string { Length: > 0 } route ? Editor.Library?.Find<RouteAsset>(route)?.Name ?? _localizer.Format("RouteGone", route) : _localizer["RouteChoose"];
            case SettingKind.Npc:
                return setting.GetValue(draft) is int npc ? Editor.NpcNames?.Npc(npc) ?? _localizer.Format("NpcNumber", npc) : _localizer["NpcNone"];
            case SettingKind.NpcReply:
                return setting.GetValues(draft) is { Count: > 0 } replies ? _localizer.Summary(replies.Cast<int>().Select(reply => Editor.NpcNames?.Reply(reply, 28) ?? _localizer.Format("RepliesReply", reply))) : _localizer["NpcRepliesNone"];
        }

        if (!setting.IsList)
            return setting.GetValue(draft) is int id ? _name(setting.Kind, id) : "…";

        var ids = setting.GetValues(draft).Cast<int>().ToList();

        if (ids.Count == 0)
            return setting.Kind is SettingKind.Monster ? _localizer["RouteEveryGroup"] : "…";

        var shown = string.Join(", ", ids.Take(3).Select(item => _name(setting.Kind, item)));

        return ids.Count > 3 ? _localizer.Format("RouteAndMore", shown, ids.Count - 3) : shown;
    }
}
