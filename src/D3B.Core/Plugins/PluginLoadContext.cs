using System.Reflection;
using System.Runtime.Loader;

namespace D3B.Core.Plugins;

/// <summary>The load context of one plugin: its own assemblies, and the host's wherever the host has them.</summary>
/// <remarks>
/// The context is not collectible: a plugin loads once and stays until the host stops, so nothing it touches has to
/// be written for unloading (D6).
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly AssemblyLoadContext _host = GetLoadContext(typeof(PluginLoadContext).Assembly) ?? Default;

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string name, string assemblyPath)
        : base(name, isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(assemblyPath);
    }

    private static Assembly? FromHost(AssemblyName assemblyName)
    {
        try
        {
            return _host.LoadFromAssemblyName(assemblyName);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (FileLoadException)
        {
            return null;
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // The host's copy wins, so that the plugin and the host share D3B.Core's types: a module from a second copy
        // of D3B.Core would not be an IModule to the host.
        if (FromHost(assemblyName) is { } shared)
            return shared;

        return _resolver.ResolveAssemblyToPath(assemblyName) is { } path ? LoadFromAssemblyPath(path) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        return _resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } path ? LoadUnmanagedDllFromPath(path) : nint.Zero;
    }
}
