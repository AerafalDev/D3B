using Lua;

namespace D3B.Scripting;

/// <summary>A script's error, which ends its activity: where it is in the script, and what (D103).</summary>
/// <remarks>Its message reads as Lua's do, <c>mineur.lua:42: attempt to index a nil value</c>, so that the app shows it as written.</remarks>
public sealed class ScriptException : Exception
{
    // LuaCSharp prefixes the messages of its runtime errors; the error itself follows.
    private const string LuaCSharpPrefix = "Lua-CSharp: ";

    /// <summary>Gets the script's stack when it failed, innermost first, if Lua gave one.</summary>
    public string? Traceback { get; }

    /// <summary>Initializes a new instance of the <see cref="ScriptException"/> class.</summary>
    public ScriptException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ScriptException"/> class.</summary>
    /// <param name="message">What failed, and where.</param>
    public ScriptException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ScriptException"/> class.</summary>
    /// <param name="message">What failed, and where.</param>
    /// <param name="innerException">The error Lua raised.</param>
    public ScriptException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private ScriptException(string message, string? traceback, Exception innerException)
        : base(message, innerException)
    {
        Traceback = traceback;
    }

    /// <summary>Tells whether LuaCSharp raised an exception for the script's own error: one it compiles or runs, not a cancellation.</summary>
    internal static bool IsScriptError(Exception exception)
    {
        return exception is LuaRuntimeException or LuaCompileException or LuaParseException;
    }

    /// <summary>The script's error a Lua exception tells: a runtime error with its stack, or a compile error with its place.</summary>
    internal static ScriptException From(Exception exception)
    {
        if (exception is LuaRuntimeException runtime)
        {
            string? traceback;

            try
            {
                traceback = runtime.LuaTraceback?.ToString();
            }
            catch (IndexOutOfRangeException)
            {
                // LuaCSharp 0.5.8 fails to write some stacks, measured on cancellations inside a function (D103).
                traceback = null;
            }

            var message = runtime.Message.StartsWith(LuaCSharpPrefix, StringComparison.Ordinal) ? runtime.Message[LuaCSharpPrefix.Length..] : runtime.Message;

            return new ScriptException(message, traceback, exception);
        }

        return new ScriptException(exception.Message, null, exception);
    }
}
