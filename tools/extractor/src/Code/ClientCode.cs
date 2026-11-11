using System.Buffers.Binary;
using System.Reflection;
using Cpp2IL.Core;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.Logging;

namespace D3B.Extractor.Code;

/// <summary>The installed client's IL2CPP code and metadata, opened with Cpp2IL, for the names its enums give.</summary>
/// <remarks>
/// Cpp2IL keeps the client it opened in static state: a process holds one open at a time, so that another opening, such
/// as a test running beside, waits for this one to be disposed.
/// </remarks>
internal sealed class ClientCode : IDisposable
{
    private const uint MetadataMagic = 0xFAB11BAF;

    // The metadata version Cpp2IL was measured to read on 3.7.13.17: another one fails here, by its number.
    private const int MetadataVersion = 39;

    private static readonly SemaphoreSlim _gate = new(1, 1);

    private static bool _initialized;

    private readonly ApplicationAnalysisContext _application;
    private bool _disposed;

    private ClientCode(ApplicationAnalysisContext application)
    {
        _application = application;
    }

    /// <summary>Opens the client's code and metadata.</summary>
    /// <exception cref="InvalidDataException">The client lacks a file of its code, its metadata is not of the version measured, or Cpp2IL cannot read it.</exception>
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

        if (version is not MetadataVersion)
        {
            throw new InvalidDataException($"The client's IL2CPP metadata is version {version}; the extractor reads version {MetadataVersion}.");
        }

        _gate.Wait();

        try
        {
            // Cpp2IL registers its plugins once a process: a second Init fails. It loads them from a folder it is given,
            // the current one by default: the tool names one of its own output, which holds none, so that no assembly
            // lying where the tool is run from is loaded.
            if (!_initialized)
            {
                LibLogger.Writer = new SilentLogWriter();
                Cpp2IlApi.Init(Path.Combine(AppContext.BaseDirectory, "cpp2il-plugins"));
                _initialized = true;
            }

            Cpp2IlApi.InitializeLibCpp2Il(binary, metadata, Cpp2IlApi.DetermineUnityVersion(player, Path.Combine(client.Root, "Dofus_Data")), false);

            var application = Cpp2IlApi.CurrentAppContext ?? throw new InvalidDataException("Cpp2IL opened the client but built no application.");

            return new ClientCode(application);
        }
        catch (Exception exception)
        {
            try
            {
                Cpp2IlApi.ResetInternalState();
            }
            finally
            {
                _gate.Release();
            }

            throw exception is InvalidDataException ? exception : new InvalidDataException($"Cpp2IL cannot read the client's code: {exception.Message}", exception);
        }
    }

    /// <summary>A member's value as the bits it sets, whatever the enum's underlying type, without sign extension.</summary>
    private static ulong ValueOf(string enumName, string member, object? value)
    {
        return value switch
        {
            sbyte number => (byte)number,
            byte number => number,
            short number => (ushort)number,
            ushort number => number,
            int number => (uint)number,
            uint number => number,
            long number => (ulong)number,
            ulong number => number,
            _ => throw new InvalidDataException($"{enumName}.{member} holds {value}, which is no integer."),
        };
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
            .Select(field => (field.Name, ValueOf(type.FullName, field.Name, field.ConstantValue)));

        return Code.FlagEnum.From(type.FullName, members);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            Cpp2IlApi.ResetInternalState();
        }
        finally
        {
            _gate.Release();
        }
    }

}
