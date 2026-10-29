using Lua.IO;

namespace D3B.Scripting.Sandbox;

/// <summary>The standard streams a script sees: an empty input, and outputs that keep nothing.</summary>
/// <remarks>A script prints through <c>print</c> and <c>global</c>, which write to its console; the process's own console is not its.</remarks>
internal sealed class NoStandardIO : ILuaStandardIO
{
    public ILuaStream Input { get; } = ILuaStream.CreateFromString("");

    public ILuaStream Output { get; } = ILuaStream.CreateFromStream(Stream.Null, LuaFileOpenMode.Write);

    public ILuaStream Error { get; } = ILuaStream.CreateFromStream(Stream.Null, LuaFileOpenMode.Write);
}
