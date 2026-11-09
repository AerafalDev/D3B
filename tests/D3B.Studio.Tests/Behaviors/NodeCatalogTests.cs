using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Studio.Behaviors;
using D3B.Studio.Behaviors.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Studio.Tests.Behaviors;

public sealed class NodeCatalogTests : IAsyncDisposable
{
    private readonly TestBehaviors _behaviors = new();

    public ValueTask DisposeAsync()
    {
        return _behaviors.DisposeAsync();
    }

    [Fact]
    public void D3BsNodesTheModulesAndTheActivitiesAreInTheCatalog()
    {
        var nodes = _behaviors.Nodes.All;

        Assert.Equal([NodeCatalog.Branch, NodeCatalog.SetVariable, NodeCatalog.GetVariable], nodes.Take(3).Select(node => node.Id));
        Assert.Equal([NodeRole.Activity, NodeRole.Activity], nodes.TakeLast(2).Select(node => node.Role));
        Assert.Equal(NodeRole.Condition, nodes.Single(node => node.Id == "pods.atLeast").Role);
        Assert.Equal(NodeRole.Event, nodes.Single(node => node.Id == "fight.over").Role);
        Assert.True(nodes.Single(node => node.Id == "note").IsInstant);
        Assert.False(nodes.Single(node => node.Id == "wait").IsInstant);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public void EveryNodeIsASentenceWithItsHelpInBothLanguages(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);

        foreach (var node in _behaviors.Nodes.All.Where(node => node.Role is not NodeRole.Activity))
        {
            Assert.DoesNotContain("[", node.Sentence.In(culture), StringComparison.Ordinal);
            Assert.DoesNotContain("[", node.Help!.In(culture), StringComparison.Ordinal);
            Assert.All(node.Values.All, value => Assert.DoesNotContain("[", value.Name.In(culture), StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ASentenceNamingAValueNotDeclaredStopsTheHost()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddNode<PodsAtLeast, PercentValues>("pods.other", LocalizedText.Invariant("Pods at {Share}")));
    }

    [Fact]
    public void TwoNodesOfOneIdStopTheHost()
    {
        var services = new ServiceCollection();

        CoreNodes.Register(services);

        Assert.Throws<InvalidOperationException>(() => services.AddNode<PodsAtLeast, PercentValues>("pods.atLeast", LocalizedText.Invariant("Pods")));
        Assert.Throws<InvalidOperationException>(() => new NodeCatalog(services.BuildServiceProvider().GetServices<NodeDefinition>(), new ActivityCatalog([new ActivityDefinition("wait", "Wait", _ => new D3B.Bot.Tests.Activities.TestActivity())]), services.BuildServiceProvider()).All.Count);
    }
}
