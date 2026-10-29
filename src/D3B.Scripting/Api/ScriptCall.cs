using System.Globalization;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using Lua;

namespace D3B.Scripting.Api;

/// <summary>A call a script made to one of its API's functions: its arguments, read as the function expects them, and its results.</summary>
/// <remarks>
/// The bots sold call their namespaces both ways, <c>global:delay(500)</c> and <c>global.delay(500)</c>, and scripts mix
/// them: the namespace a <c>:</c> call passes first is skipped, so that both read the same arguments.
/// </remarks>
internal readonly struct ScriptCall
{
    private readonly LuaFunctionExecutionContext _context;
    private readonly int _first;

    /// <summary>Gets the function's name as the script calls it, such as <c>map:moveToCell</c>.</summary>
    public string Function { get; }

    /// <summary>Gets what the script's functions act on.</summary>
    public ScriptBinding Binding { get; }

    /// <summary>Gets the context of the hook running now, to act through.</summary>
    /// <exception cref="LuaRuntimeException">No hook that acts runs, such as in <c>isFull</c>.</exception>
    public BotContext Context => Binding.CurrentContext ?? throw Error(Binding.WhyNoContext);

    /// <summary>Gets the character's state the hook running now reads.</summary>
    /// <exception cref="LuaRuntimeException">No hook runs, such as while the script's text runs.</exception>
    public CharacterState Character => Binding.CurrentState ?? throw Error(ScriptBinding.WhyNoState);

    /// <summary>Gets the number of arguments the script passed.</summary>
    public int Count => _context.ArgumentCount - _first;

    /// <param name="context">The call as LuaCSharp passes it.</param>
    /// <param name="self">The function's namespace, which a <c>:</c> call passes first, or <see langword="null"/> for a global function.</param>
    /// <param name="function">The function's name as the script calls it.</param>
    /// <param name="binding">What the script's functions act on.</param>
    public ScriptCall(LuaFunctionExecutionContext context, LuaTable? self, string function, ScriptBinding binding)
    {
        _context = context;
        _first = self is not null && context.ArgumentCount > 0 && context.GetArgument(0).TryRead<LuaTable>(out var table) && ReferenceEquals(table, self) ? 1 : 0;
        Function = function;
        Binding = binding;
    }

    /// <summary>Gets an argument, nil when the script passed none there.</summary>
    public LuaValue Argument(int index)
    {
        return index < Count ? _context.GetArgument(_first + index) : LuaValue.Nil;
    }

    /// <summary>Reads an argument that must be a whole number, as a cell or an id is; a string of digits counts, as scripts often write ids.</summary>
    public long Integer(int index)
    {
        var value = Argument(index);

        if (value.TryRead<double>(out var number) && number == Math.Floor(number) && number is >= long.MinValue and <= long.MaxValue)
            return (long)number;

        if (value.TryRead<string>(out var text) && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        throw BadArgument(index, "whole number", value);
    }

    /// <summary>Reads an argument that must be a whole number, or gives a default when the script passed none.</summary>
    public long Integer(int index, long otherwise)
    {
        return Argument(index).Type is LuaValueType.Nil ? otherwise : Integer(index);
    }

    /// <summary>Reads an argument that must be a whole number between bounds.</summary>
    public int Integer(int index, int minimum, int maximum)
    {
        var value = Integer(index);

        if (value < minimum || value > maximum)
            throw Error($"bad argument #{index + 1} ({value} is not between {minimum} and {maximum})");

        return (int)value;
    }

    /// <summary>Reads an argument that must be a string.</summary>
    public string Text(int index)
    {
        var value = Argument(index);

        if (value.TryRead<string>(out var text))
            return text;

        throw BadArgument(index, "string", value);
    }

    /// <summary>Joins every argument from one on, as <c>print</c> does: what a script prints may be anything.</summary>
    public string Joined(int from = 0)
    {
        var parts = new string[Math.Max(0, Count - from)];

        for (var i = 0; i < parts.Length; i++)
            parts[i] = Argument(from + i).ToString();

        return string.Join('\t', parts);
    }

    public int Return()
    {
        return _context.Return();
    }

    public int Return(LuaValue value)
    {
        return _context.Return(value);
    }

    public int Return(LuaValue first, LuaValue second)
    {
        return _context.Return(first, second);
    }

    /// <summary>Reads an argument that may be a table, or nil.</summary>
    public LuaTable? Table(int index)
    {
        var value = Argument(index);

        if (value.Type is LuaValueType.Nil)
            return null;

        return value.TryRead<LuaTable>(out var table) ? table : throw BadArgument(index, "table", value);
    }

    /// <summary>An error of the script, raised where it called the function, which <c>pcall</c> may catch.</summary>
    public LuaRuntimeException Error(string message)
    {
        return new LuaRuntimeException(_context.State, $"{Function}: {message}", 1);
    }

    private LuaRuntimeException BadArgument(int index, string expected, LuaValue value)
    {
        return Error($"bad argument #{index + 1} ({expected} expected, got {value.TypeToString()})");
    }
}
