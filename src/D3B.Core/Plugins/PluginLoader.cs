using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
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
        var root = FindRootModule(name, assemblyPath, assembly);
        var attribute = root.GetCustomAttribute<PluginAttribute>()
            ?? throw new PluginLoadException($"The plugin '{name}' marks {root.FullName} with an attribute of its own named [Plugin], not D3B.Core's.");
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

    private static Type FindRootModule(string name, string assemblyPath, Assembly assembly)
    {
        var roots = RootModuleNames(name, assemblyPath);

        if (roots.Count == 0)
            throw new PluginLoadException($"The plugin '{name}' has no public class marked [Plugin].");

        if (roots.Count > 1)
            throw new PluginLoadException($"The plugin '{name}' has several classes marked [Plugin]: {string.Join(", ", roots)}.");

        Type root;

        try
        {
            root = assembly.GetType(roots[0], throwOnError: true)!;
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            throw new PluginLoadException($"The plugin '{name}' references an assembly that neither the host nor the plugin holds.", exception);
        }

        if (!typeof(IModule).IsAssignableFrom(root) || root.IsAbstract || root.GetConstructor(Type.EmptyTypes) is null)
            throw new PluginLoadException($"The plugin '{name}' marks {root.FullName} [Plugin], which is not a concrete IModule with a public parameterless constructor.");

        return root;
    }

    /// <summary>Reads the public classes marked [Plugin] from the assembly's metadata, without loading any type.</summary>
    /// <remarks>
    /// Loading every public type, as reflection would to look for the attribute, loads every assembly their declarations
    /// name: a plugin's page, whose types derive from the app's UI framework, would then stop a host without the app from
    /// loading the plugin (D97).
    /// </remarks>
    private static List<string> RootModuleNames(string name, string assemblyPath)
    {
        try
        {
            using var file = File.OpenRead(assemblyPath);
            using var image = new PEReader(file);
            var metadata = image.GetMetadataReader();
            var roots = new List<string>();

            foreach (var handle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(handle);

                if ((type.Attributes & TypeAttributes.VisibilityMask) is not TypeAttributes.Public)
                    continue;

                if (type.GetCustomAttributes().Any(attribute => IsPluginAttribute(metadata, metadata.GetCustomAttribute(attribute))))
                    roots.Add(FullName(metadata, type));
            }

            return roots;
        }
        catch (BadImageFormatException exception)
        {
            throw new PluginLoadException($"The plugin '{name}' is not a .NET assembly.", exception);
        }
    }

    private static bool IsPluginAttribute(MetadataReader metadata, CustomAttribute attribute)
    {
        var type = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default,
        };
        var (space, name) = type.Kind switch
        {
            HandleKind.TypeReference when metadata.GetTypeReference((TypeReferenceHandle)type) is var reference => (reference.Namespace, reference.Name),
            HandleKind.TypeDefinition when metadata.GetTypeDefinition((TypeDefinitionHandle)type) is var definition => (definition.Namespace, definition.Name),
            _ => (default, default),
        };

        return !name.IsNil
            && metadata.StringComparer.Equals(space, typeof(PluginAttribute).Namespace!)
            && metadata.StringComparer.Equals(name, nameof(PluginAttribute));
    }

    private static string FullName(MetadataReader metadata, TypeDefinition type)
    {
        var space = metadata.GetString(type.Namespace);
        var name = metadata.GetString(type.Name);

        return space.Length > 0 ? $"{space}.{name}" : name;
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
