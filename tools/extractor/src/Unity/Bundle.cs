using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace D3B.Extractor.Unity;

/// <summary>A bundle of the client, opened through AssetsTools.NET, whose objects are read through their type trees.</summary>
/// <remarks>
/// Every serialized file of the client carries its type trees (Unity 6000.3, <c>main:docs/client.md</c>), so no IL2CPP
/// dump is needed. AssetsTools.NET stays inside this class: the rest of the extractor sees type trees and bytes.
/// </remarks>
internal sealed class Bundle : IDisposable
{
    private const uint AlignFlag = 0x4000;

    private readonly AssetsManager _manager = new();
    private BundleFileInstance? _bundle;
    private readonly List<AssetsFileInstance> _files = [];

    /// <summary>Gets the bundle's full path.</summary>
    public string Path { get; }

    private Bundle(string path)
    {
        Path = path;
    }

    /// <summary>Opens a bundle.</summary>
    /// <exception cref="InvalidDataException">The file is missing or is not a bundle AssetsTools.NET reads.</exception>
    public static Bundle Open(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"The client has no {path}.");
        }

        var bundle = new Bundle(path);

        try
        {
            var file = bundle._manager.LoadBundleFile(path, true);

            bundle._bundle = file;

            var entries = file.file.BlockAndDirInfo.DirectoryInfos;

            // The directory flags say which entries are serialized files: AssetsTools.NET's own guess takes some .resS
            // files for serialized ones (main:docs/client.md).
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsSerialized)
                {
                    bundle._files.Add(bundle._manager.LoadAssetsFileFromBundle(file, i, false));
                }
            }

            return bundle;
        }
        catch (Exception exception)
        {
            bundle.Dispose();

            throw exception is InvalidDataException ? exception : new InvalidDataException($"{path} is not a bundle the extractor reads: {exception.Message}", exception);
        }
    }

    private static TypeTree TreeOf(TypeTreeType type, string name)
    {
        var strings = type.StringBufferBytes;
        var common = TypeTreeType.COMMON_STRING_TABLE;
        var nodes = type.Nodes.Select(node => new FlatNode
        {
            Level = node.Level,
            Type = node.GetTypeString(strings, common),
            Name = node.GetNameString(strings, common),
            ByteSize = node.ByteSize,
            Align = (node.MetaFlags & AlignFlag) is not 0,
            IsArray = node.TypeFlags.HasFlag(TypeTreeNodeFlags.Array),
            IsRegistry = node.TypeFlags.HasFlag(TypeTreeNodeFlags.Registry),
        });

        return TypeTree.Build(name, [.. nodes]);
    }

    private static Dictionary<string, TypeTree> ReferenceTypesOf(AssetsFileMetadata metadata)
    {
        var types = new Dictionary<string, TypeTree>(StringComparer.Ordinal);

        foreach (var type in metadata.RefTypes ?? [])
        {
            if (type.TypeReference is not { } reference)
            {
                continue;
            }

            var name = reference.Namespace.Length is 0 ? reference.ClassName : $"{reference.Namespace}.{reference.ClassName}";

            types.TryAdd(name, TreeOf(type, reference.ClassName));
        }

        return types;
    }

    private static string NameOf(TypeTree tree, byte[] data)
    {
        // A MonoBehaviour's name follows its game object and enabled flag and its script, all of fixed size.
        var reader = new ObjectReader(data, tree);

        foreach (var child in tree[0].Children)
        {
            if (tree[child].Name == "m_Name")
            {
                return reader.ReadString(child);
            }

            reader.Skip(child);
        }

        return "";
    }
    /// <summary>The script objects of the bundle, in file order, each read when the caller reaches it.</summary>
    public IEnumerable<SerializedObject> MonoBehaviours()
    {
        return Objects(AssetClassID.MonoBehaviour);
    }

    /// <summary>The objects of a class, such as <c>Texture2D</c>, in file order, each read when the caller reaches it.</summary>
    public IEnumerable<SerializedObject> Objects(AssetClassID type)
    {
        foreach (var file in _files)
        {
            var metadata = file.file.Metadata;
            var references = ReferenceTypesOf(metadata);
            var trees = new Dictionary<int, TypeTree>();

            foreach (var info in metadata.GetAssetsOfType(type))
            {
                if (!trees.TryGetValue(info.TypeIdOrIndex, out var tree))
                {
                    tree = TreeOf(metadata.TypeTreeTypes[info.TypeIdOrIndex], $"{System.IO.Path.GetFileName(Path)} type {info.TypeIdOrIndex}");
                    trees.Add(info.TypeIdOrIndex, tree);
                }

                var reader = file.file.Reader;

                reader.Position = info.GetAbsoluteByteOffset(file.file);

                var data = reader.ReadBytes(checked((int)info.ByteSize));

                yield return new SerializedObject { PathId = info.PathId, Name = NameOf(tree, data), Tree = tree, Data = data, ReferenceTypes = references };
            }
        }
    }

    /// <summary>Reads a range of a stream file of the bundle, such as the <c>.resS</c> that holds a texture's texels.</summary>
    /// <param name="streamPath">The path a <c>m_StreamData</c> names, such as <c>archive:/CAB-x/CAB-x.resS</c>.</param>
    /// <param name="offset">Where the range starts in the stream file.</param>
    /// <param name="size">The range's length.</param>
    /// <exception cref="InvalidDataException">The bundle holds no such file, or the range runs past its end.</exception>
    public byte[] ReadStream(string streamPath, long offset, int size)
    {
        var file = _bundle!.file;
        var name = streamPath[(streamPath.LastIndexOf('/') + 1)..];
        var index = file.BlockAndDirInfo.DirectoryInfos.FindIndex(entry => entry.Name == name);

        if (index is -1)
        {
            throw new InvalidDataException($"{Path} holds no {name}.");
        }

        file.GetFileRange(index, out var start, out var length);

        if (offset < 0 || size < 0 || offset + size > length)
        {
            throw new InvalidDataException($"{name} holds {length} bytes, not {offset + size}.");
        }

        var reader = file.DataReader;

        reader.Position = start + offset;

        return reader.ReadBytes(size);
    }

    public void Dispose()
    {
        _manager.UnloadAll(true);
    }

}
