using System.Runtime.Loader;
using D3B.Core.Modules;
using D3B.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Core.Tests.Plugins;

public sealed class PluginLoadingTests
{
    private const string SampleName = "D3B.Samples.Hello";

    // The build copies the sample plugin there, as a host's plugins folder would hold it.
    private static string SamplePlugins => Path.Combine(AppContext.BaseDirectory, "plugins");

    private static string CreatePluginsDirectory()
    {
        // Under the test output rather than the user's temporary folder: a loaded plugin stays loaded, so its files
        // stay locked until the test run ends.
        return Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "test-plugins", Guid.NewGuid().ToString("N"))).FullName;
    }

    private static void CopySample(string plugins, string name)
    {
        var folder = Directory.CreateDirectory(Path.Combine(plugins, name));

        foreach (var file in Directory.GetFiles(Path.Combine(SamplePlugins, SampleName)))
            File.Copy(file, Path.Combine(folder.FullName, Path.GetFileName(file).Replace(SampleName, name, StringComparison.Ordinal)));
    }

    [Fact]
    public void TheSamplePluginLoadsIntoAContextOfItsOwn()
    {
        var module = Assert.Single(TestBots.Create().AddPlugins(SamplePlugins).Modules);
        var context = AssemblyLoadContext.GetLoadContext(module.GetType().Assembly);

        Assert.NotNull(context);
        Assert.NotSame(AssemblyLoadContext.Default, context);
        Assert.Equal(SampleName, context.Name);
        Assert.False(context.IsCollectible);
    }

    [Fact]
    public void ThePluginSharesTheHostCopyOfCore()
    {
        var module = Assert.Single(TestBots.Create().AddPlugins(SamplePlugins).Modules);
        var context = AssemblyLoadContext.GetLoadContext(module.GetType().Assembly)!;

        Assert.Contains(typeof(IModule), module.GetType().GetInterfaces());
        Assert.DoesNotContain(context.Assemblies, assembly => assembly.GetName().Name == typeof(IModule).Assembly.GetName().Name);
    }

    [Fact]
    public void ThePluginIdentityComesFromItsAttributeAndItsAssembly()
    {
        using var host = TestBots.Create().AddPlugins(SamplePlugins).Build();

        var plugin = Assert.Single(host.Services.GetServices<PluginInfo>());

        Assert.Equal("d3b.samples.hello", plugin.Id);
        Assert.Equal("Hello", plugin.Name);
        Assert.Equal("Aerafal", plugin.Author);
        Assert.False(string.IsNullOrWhiteSpace(plugin.Version));
        Assert.Equal(Path.Combine(SamplePlugins, SampleName), plugin.Directory);
    }

    [Fact]
    public async Task ThePluginRunsInTheHost()
    {
        var bot = TestBots.Create().AddPlugins(SamplePlugins);

        bot.Services.AddFakeLogging();

        using var host = bot.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains(host.Services.GetFakeLogCollector().GetSnapshot(), record => record.Message == "Hello from a plugin");
    }

    [Fact]
    public void AMissingPluginsDirectoryHoldsNoPlugin()
    {
        var bot = TestBots.Create().AddPlugins(Path.Combine(AppContext.BaseDirectory, "no-plugins"));

        Assert.Empty(bot.Modules);
    }

    [Fact]
    public void AFolderWithoutItsAssemblyIsRejected()
    {
        var plugins = CreatePluginsDirectory();

        Directory.CreateDirectory(Path.Combine(plugins, "Empty"));

        var error = Assert.Throws<PluginLoadException>(() => TestBots.Create().AddPlugins(plugins));

        Assert.Contains("Empty.dll", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAssemblyWithoutAPluginClassIsRejected()
    {
        var plugins = CreatePluginsDirectory();
        var folder = Directory.CreateDirectory(Path.Combine(plugins, "Core"));

        File.Copy(typeof(IModule).Assembly.Location, Path.Combine(folder.FullName, "Core.dll"));

        var error = Assert.Throws<PluginLoadException>(() => TestBots.Create().AddPlugins(plugins));

        Assert.Contains("[Plugin]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoPluginsWithTheSameIdAreRejected()
    {
        var plugins = CreatePluginsDirectory();

        CopySample(plugins, "First");
        CopySample(plugins, "Second");

        var error = Assert.Throws<PluginLoadException>(() => TestBots.Create().AddPlugins(plugins));

        Assert.Contains("d3b.samples.hello", error.Message, StringComparison.Ordinal);
    }
}
