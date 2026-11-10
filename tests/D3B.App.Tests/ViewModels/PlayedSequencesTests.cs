using System.Collections.Immutable;
using System.Text.Json.Nodes;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Routes;
using D3B.App.ViewModels.Sequences;
using D3B.App.ViewModels.Settings;
using D3B.Studio.Assets;
using D3B.Studio.Behaviors;

namespace D3B.App.Tests.ViewModels;

[UiThread]
public sealed class PlayedSequencesTests : IAsyncDisposable
{
    private readonly TestStore _store = new();
    private readonly Localizer _localizer = new(Localizer.English);

    public ValueTask DisposeAsync()
    {
        return _store.DisposeAsync();
    }

    private static NodeCall Step(string node, params (string Key, JsonNode? Value)[] values)
    {
        return new NodeCall(node, values.ToImmutableSortedDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal));
    }

    private async Task<SequencesViewModel> PageAsync()
    {
        await _store.OpenedAsync();

        var page = new SequencesViewModel(_localizer, new Dialogs(), new Toasts());

        page.Refresh(_store.Host.Services);

        return page;
    }

    private async Task<BehaviorAsset> ListAsync(string name, params NodeCall[] steps)
    {
        return await _store.Assets.SaveAsync(BehaviorSequences.Write(new BehaviorAsset { Id = AssetLibrary.NewId(), Name = name }, new BehaviorSequence([.. steps], StepRepeat.Once)), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AStepPlaysASequencePickedAmongTheOtherListsNeverTyped()
    {
        var page = await PageAsync();
        var bank = await ListAsync("Bank", Step("note", ("Text", "bank")));
        var other = await ListAsync("Other", Step("stop"));
        var editor = page.OpenList(other.Id)!;

        editor.ChooseStepCommand.Execute(null);
        Assert.IsType<RouteActionPickerViewModel>(page.Dialogs.Current).Actions.Single(action => action.Name == "Play a sequence").AddCommand.Execute(null);

        var step = editor.Steps[1];

        Assert.Equal(BehaviorSequences.Play, step.NodeId);
        Assert.Equal("Play the sequence choose a sequence", step.Sentence);
        Assert.True(step.HasProblems);
        Assert.False(editor.CanSave);

        var field = Assert.IsType<SequenceFieldViewModel>(step.Editor!.FieldOf("Sequence"));

        field.PickCommand.Execute(null);

        var picker = Assert.IsType<RoutePickerViewModel>(page.Dialogs.Current);

        // The list being edited is not offered to itself.
        Assert.Equal(["Bank"], picker.Routes.Select(choice => choice.Name));
        Assert.Equal(["1 step"], picker.Routes.Select(choice => choice.Detail));
        picker.Routes[0].ChooseCommand.Execute(null);

        Assert.Equal("Play the sequence Bank", step.Sentence);
        Assert.True(editor.CanSave);

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Equal([bank.Id], _store.Assets.Find<BehaviorAsset>(other.Id)!.Uses());
    }

    [Fact]
    public async Task ASequenceThatPlaysTheListBeingEditedIsNotOfferedToIt()
    {
        var page = await PageAsync();
        var deep = await ListAsync("Deep", Step("note", ("Text", "deep")));
        var middle = await ListAsync("Middle", Step(BehaviorSequences.Play, ("Sequence", deep.Id)));
        var editor = page.OpenList(deep.Id)!;

        editor.AddStepCommand.Execute(BehaviorSequences.Play);

        var field = Assert.IsType<SequenceFieldViewModel>(editor.Steps[1].Editor!.FieldOf("Sequence"));

        field.PickCommand.Execute(null);

        // Middle plays Deep: Deep playing Middle would never end. Nothing else is left to offer.
        var picker = Assert.IsType<RoutePickerViewModel>(page.Dialogs.Current);

        Assert.Empty(picker.Routes);
        Assert.DoesNotContain(middle.Name, picker.Routes.Select(choice => choice.Name));
    }

    [Fact]
    public async Task ASequenceThatIsNoLongerInTheProjectIsSaidMissing()
    {
        var page = await PageAsync();
        var list = await ListAsync("Gone", Step(BehaviorSequences.Play, ("Sequence", "nowhere")));
        var editor = page.OpenList(list.Id)!;

        Assert.Equal("Play the sequence nowhere (missing)", editor.Steps[0].Sentence);
    }
}
