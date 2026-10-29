using Lua;

namespace D3B.Scripting.Api;

/// <summary>What an action of the API returns, as Lua's own functions do: <c>true</c>, or <c>false</c> and why.</summary>
/// <remarks>Why is the behavior's outcome, its name in lower camel case as the API's are, such as <c>"noPath"</c>.</remarks>
internal static class Outcomes
{
    /// <summary>Returns an outcome: <c>true</c> when it is the success, else <c>false</c> and its name.</summary>
    public static int Return<T>(ScriptCall call, T outcome, T success)
        where T : struct, Enum
    {
        return EqualityComparer<T>.Default.Equals(outcome, success) ? call.Return(true) : call.Return(false, NameOf(outcome));
    }

    /// <summary>The name of an enumeration's value in lower camel case.</summary>
    public static string NameOf<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();

        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>A value as Lua reads a whole number, or nil.</summary>
    public static LuaValue Of(long? value)
    {
        return value is { } number ? number : LuaValue.Nil;
    }
}
