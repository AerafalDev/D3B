using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using Cpp2IL.Core;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.Logging;

namespace D3B.Extractor.Code;

/// <summary>The installed client's IL2CPP code and metadata, opened with Cpp2IL, for the names its enums give.</summary>
/// <remarks>Cpp2IL keeps the client it opened in static state: a process opens one client at a time.</remarks>
internal sealed class ClientCode : IDisposable
{
    private const uint MetadataMagic = 0xFAB11BAF;

    private readonly ApplicationAnalysisContext _application;

    /// <summary>Gets the version of the client's <c>global-metadata.dat</c>, from its header: 39 on 3.7.13.17.</summary>
    public int MetadataVersion { get; }

    private ClientCode(ApplicationAnalysisContext application, int metadataVersion)
    {
        _application = application;
        MetadataVersion = metadataVersion;
    }

    /// <summary>Opens the client's code and metadata.</summary>
    /// <exception cref="InvalidDataException">The client lacks a file of its code, or its metadata is not IL2CPP's.</exception>
    public static ClientCode Open(ClientInstall client)
    {
        var binary = Path.Combine(client.Root, "GameAssembly.dll");
        var metadata = Path.Combine(client.Root, "Dofus_Data", "il2cpp_data", "Metadata", "global-metadata.dat");
        var player = Path.Combine(client.Root, "UnityPlayer.dll");

        foreach (var file in new[] { binary, metadata, player })
        {
            if (!File.Exists(file))
            {
                throw new InvalidDataException($"The client has no {file}.");
            }
        }

        var version = MetadataVersionOf(metadata);

        LibLogger.Writer = new SilentLogWriter();

        // Cpp2IL loads plugins from a folder it is given, the current one by default: the tool names one of its own
        // output, which holds none, so that no assembly lying where the tool is run from is loaded.
        Cpp2IlApi.Init(Path.Combine(AppContext.BaseDirectory, "cpp2il-plugins"));
        Cpp2IlApi.InitializeLibCpp2Il(binary, metadata, Cpp2IlApi.DetermineUnityVersion(player, Path.Combine(client.Root, "Dofus_Data")), false);

        var application = Cpp2IlApi.CurrentAppContext ?? throw new InvalidDataException("Cpp2IL opened the client but built no application.");

        return new ClientCode(application, version);
    }

    /// <summary>Reads the flags enum of a name, such as <c>MapInformationFlags</c>, wherever the client declares it.</summary>
    /// <exception cref="InvalidDataException">No type or several bear the name, or it is no enum.</exception>
    public FlagEnum FlagEnum(string name)
    {
        var types = _application.Assemblies.SelectMany(assembly => assembly.Types).Where(type => type.Name == name).ToList();

        if (types.Count is not 1)
        {
            throw new InvalidDataException($"The client's code declares {types.Count} types named {name}, where the extractor reads one.");
        }

        var type = types[0];

        if (!type.IsEnumType)
        {
            throw new InvalidDataException($"{type.FullName} is no enum.");
        }

        var members = type.Fields
            .Where(field => field.IsStatic && field.Attributes.HasFlag(FieldAttributes.Literal))
            .Select(field => (field.Name, unchecked((ulong)Convert.ToInt64(field.ConstantValue, CultureInfo.InvariantCulture))));

        return Code.FlagEnum.From(type.FullName, members);
    }

    public void Dispose()
    {
        Cpp2IlApi.ResetInternalState();
    }

    private static int MetadataVersionOf(string path)
    {
        Span<byte> header = stackalloc byte[8];

        using (var stream = File.OpenRead(path))
        {
            stream.ReadExactly(header);
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != MetadataMagic)
        {
            throw new InvalidDataException($"{path} is not IL2CPP metadata.");
        }

        return BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
    }
}
