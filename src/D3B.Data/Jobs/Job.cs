using System.Runtime.InteropServices;

namespace D3B.Data.Jobs;

/// <summary>A profession, such as lumberjack or fisherman, from the client's <c>JobsDataRoot</c>.</summary>
/// <remarks>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Job
{
    /// <summary>The job of the skills that belong to no profession, such as using a zaap or opening a door: the client names it Base.</summary>
    public const int Base = 1;

    /// <summary>Gets the job's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Initializes a new instance of the <see cref="Job"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    public Job(int id, int nameId)
    {
        Id = id;
        NameId = nameId;
    }
}
