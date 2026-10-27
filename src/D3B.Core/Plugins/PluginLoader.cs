using System.Reflection;
using D3B.Core.Modules;

namespace D3B.Core.Plugins;

/// <summary>Reads the plugins under a directory: one folder per plugin, holding an assembly named after the folder.</summary>
internal static class PluginLoader
{
    public static IReadOnlyList<LoadedPlugin> Load(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        var plugins = new List<LoadedPlugin>();

        // Sorted, so that plugins are built in the same order on every machine.
        foreach (var folder in Directory.GetDirectories(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            var plugin = LoadFolder(folder);

            if (plugins.Find(loaded => string.Equals(loaded.Info.Id, plugin.Info.Id, StringComparison.OrdinalIgnoreCase)) is { } other)
                throw new PluginLoadException($"The plugins in '{other.Info.Directory}' and '{folder}' share the id '{plugin.Info.Id}'.");

            plugins.Add(plugin);
        }

        return plugins;
    }

    private static LoadedPlugin LoadFolder(string folder)
    {
        var name = Path.GetFileName(folder);
        var assemblyPath = Path.Combine(folder, name + ".dll");

        if (!File.Exists(assemblyPath))
            throw new PluginLoadException($"The plugin folder '{folder}' holds no '{name}.dll': a plugin folder holds an assembly named after it.");

        var assembly = LoadAssembly(name, assemblyPath);
        var root = FindRootModule(name, assembly);
        var attribute = root.GetCustomAttribute<PluginAttribute>()!;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return new LoadedPlugin
        {
            Info = new PluginInfo
            {
                Id = attribute.Id,
                Name = attribute.Name ?? name,
                Version = version ?? assembly.GetName().Version?.ToString() ?? "0.0.0",
                Author = attribute.Author,
                Directory = folder,
            },
            Module = CreateModule(name, root),
        };
    }

    private static Assembly LoadAssembly(string name, string assemblyPath)
    {
        try
        {
            return new PluginLoadContext(name, assemblyPath).LoadFromAssemblyPath(assemblyPath);
        }
        catch (BadImageFormatException exception)
        {
            throw new PluginLoadException($"The plugin '{name}' is not a .NET assembly.", exception);
        }
        catch (FileLoadException exception)
        {
            throw new PluginLoadException($"The plugin '{name}' could not be loaded.", exception);
        }
    }

    private static Type FindRootModule(string name, Assembly assembly)
    {
        Type[] types;

        try
        {
            types = assembly.GetExportedTypes();
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or TypeLoadException or ReflectionTypeLoadException)
        {
            throw new PluginLoadException($"The plugin '{name}' references an assembly that neither the host nor the plugin holds.", exception);
        }

        var roots = types.Where(type => type.IsDefined(typeof(PluginAttribute), inherit: false)).ToList();

        if (roots.Count == 0)
            throw new PluginLoadException($"The plugin '{name}' has no public class marked [Plugin].");

        if (roots.Count > 1)
            throw new PluginLoadException($"The plugin '{name}' has several classes marked [Plugin]: {string.Join(", ", roots.Select(type => type.FullName))}.");

        var root = roots[0];

        if (!typeof(IModule).IsAssignableFrom(root) || root.IsAbstract || root.GetConstructor(Type.EmptyTypes) is null)
            throw new PluginLoadException($"The plugin '{name}' marks {root.FullName} [Plugin], which is not a concrete IModule with a public parameterless constructor.");

        return root;
    }

    private static IModule CreateModule(string name, Type root)
    {
        try
        {
            return (IModule)Activator.CreateInstance(root)!;
        }
        catch (TargetInvocationException exception)
        {
            throw new PluginLoadException($"The root module of the plugin '{name}' failed to construct.", exception.InnerException ?? exception);
        }
    }
}
