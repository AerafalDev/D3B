using Lua.IO;

namespace D3B.Scripting.Sandbox;

/// <summary>The file system a script sees: none, every file refused.</summary>
/// <remarks>
/// The sandbox opens no library that reaches files, and removes the base library's functions that do (<c>dofile</c>,
/// <c>loadfile</c>); this file system refuses whatever would still ask, so that a library LuaCSharp adds later cannot open
/// one either.
/// </remarks>
internal sealed class NoFileSystem : ILuaFileSystem
{
    private const string Refused = "A script has no access to files.";

    public string DirectorySeparator => "/";

    public bool IsReadable(string path)
    {
        return false;
    }

    public ValueTask<ILuaStream> Open(string path, LuaFileOpenMode mode, CancellationToken cancellationToken)
    {
        throw new UnauthorizedAccessException(Refused);
    }

    public ValueTask Rename(string oldName, string newName, CancellationToken cancellationToken)
    {
        throw new UnauthorizedAccessException(Refused);
    }

    public ValueTask Remove(string path, CancellationToken cancellationToken)
    {
        throw new UnauthorizedAccessException(Refused);
    }

    public string GetTempFileName()
    {
        throw new UnauthorizedAccessException(Refused);
    }

    public ValueTask<ILuaStream> OpenTempFileStream(CancellationToken cancellationToken)
    {
        throw new UnauthorizedAccessException(Refused);
    }
}
