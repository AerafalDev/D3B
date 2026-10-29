using Lua;
using Lua.Platforms;
using Lua.Standard;

namespace D3B.Scripting.Sandbox;

/// <summary>Makes the Lua state a script runs in: the language and its pure libraries, nothing that reaches files, processes or the host.</summary>
/// <remarks>
/// <para>
/// A script comes from anyone, as the bots sold share theirs: it runs in the process that holds every client's game
/// connection (D91), so it must reach nothing but the bot it plays. It gets the base library without <c>dofile</c>,
/// <c>loadfile</c> and <c>load</c>, which read files or turn text and bytecode into code, and without <c>collectgarbage</c>,
/// which would drive the whole process's collector; <c>string</c> without <c>dump</c>, which only serves <c>load</c>;
/// <c>table</c>, <c>math</c> and <c>bit32</c>; and of <c>os</c> its clock and dates alone. No <c>io</c>, <c>package</c>,
/// <c>require</c> or <c>debug</c>, which reaches into other functions' locals. The platform under it refuses files, the
/// environment and an exit, should a library come to ask.
/// </para>
/// <para>
/// A script cannot hold the bot: the step's token cancels a call it awaits, and LuaCSharp checks the token as the script
/// loops, so a loop that never awaits is stopped too, measured (D103).
/// </para>
/// </remarks>
internal static class ScriptSandbox
{
    private static readonly string[] _removedGlobals = ["dofile", "loadfile", "load", "collectgarbage", "print"];
    private static readonly string[] _keptOs = ["clock", "date", "difftime", "time"];

    /// <summary>Makes a state for one script.</summary>
    /// <param name="time">The bot's clock, which <c>os.time</c> and <c>os.date</c> read.</param>
    /// <returns>The state, its globals the sandbox's alone, before the script's API is added.</returns>
    public static LuaState Create(TimeProvider time)
    {
        var platform = new LuaPlatform(new NoFileSystem(), new NoOsEnvironment(), new NoStandardIO(), time);
        var state = LuaState.Create(platform);

        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();
        state.OpenMathLibrary();
        state.OpenBitwiseLibrary();
        state.OpenOperatingSystemLibrary();

        var globals = state.Environment;

        foreach (var name in _removedGlobals)
            globals[name] = LuaValue.Nil;

        if (globals["string"].TryRead<LuaTable>(out var strings))
            strings["dump"] = LuaValue.Nil;

        var os = new LuaTable();

        if (globals["os"].TryRead<LuaTable>(out var system))
        {
            foreach (var name in _keptOs)
                os[name] = system[name];
        }

        globals["os"] = os;

        return state;
    }
}
