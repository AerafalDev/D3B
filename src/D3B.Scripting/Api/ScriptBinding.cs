using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Engine.World;
using Lua;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Scripting.Api;

/// <summary>What the script's API acts on: the hook running now, the script's console and its state.</summary>
/// <remarks>
/// <para>
/// Each hook of the activity runs as a behavior of its own, with its context (D87): the activity enters the binding as a
/// hook starts and leaves it as it ends, so that the API reads the character's state and acts through the hook's behavior.
/// <c>isFull</c> and <c>onInterrupted</c> run outside a behavior: they read, through a state alone, and never act.
/// </para>
/// <para>
/// A Lua state runs one call at a time, on one hook: nothing else touches the binding meanwhile. What the binding keeps
/// from the last context, the world, the log and the clock, serves the hooks that read.
/// </para>
/// </remarks>
internal sealed class ScriptBinding
{
    private BotContext? _context;
    private CharacterState? _state;
    private bool _reading;
    private ILogger _log = NullLogger.Instance;

    /// <summary>Gets why the script cannot read the bot now.</summary>
    public static string WhyNoState => "the bot is read in a hook, such as step(bot), not as the script's text runs";

    /// <summary>Gets the script's name.</summary>
    public string ScriptName { get; }

    /// <summary>Gets the script's console.</summary>
    public ScriptConsole Console { get; }

    /// <summary>Gets the script's Lua state.</summary>
    public LuaState State { get; }

    /// <summary>Gets the clock, the bot's once a hook ran.</summary>
    public TimeProvider Time { get; private set; } = TimeProvider.System;

    /// <summary>Gets the paths and atlas of the installed build, once a hook ran.</summary>
    public WorldPathfinder? World { get; private set; }

    /// <summary>Gets the maps of the installed build, once a hook ran.</summary>
    public GameMaps? Maps { get; private set; }

    /// <summary>Gets the character's state the hook running now reads, if one runs.</summary>
    public CharacterState? CurrentState => _context?.State ?? _state;

    /// <summary>Gets the context of the hook running now, to act through, if one that acts runs.</summary>
    public BotContext? CurrentContext => _context;

    /// <summary>Gets why the script cannot act now.</summary>
    public string WhyNoContext => _reading
        ? "isFull(bot) reads the bot, and never acts"
        : "the bot acts in a hook, such as step(bot), not as the script's text runs";

    public ScriptBinding(string scriptName, ScriptConsole console, LuaState state)
    {
        ScriptName = scriptName;
        Console = console;
        State = state;
    }

    /// <summary>Enters a hook that acts, such as <c>step</c>.</summary>
    public void Enter(BotContext context)
    {
        _context = context;
        _state = null;
        _reading = false;
        _log = context.Log;
        Time = context.Time;
        World = context.World;
        Maps = context.Maps;
    }

    /// <summary>Enters a hook that reads, such as <c>isFull</c>, on a state.</summary>
    public void EnterReading(CharacterState? state)
    {
        _context = null;
        _state = state;
        _reading = true;
    }

    public void Leave()
    {
        _context = null;
        _state = null;
        _reading = false;
    }

    /// <summary>Writes a line to the script's console and to the bot's log.</summary>
    public void Write(ScriptLineKind kind, string text)
    {
        Console.Write(new ScriptLine(Time.GetUtcNow(), kind, text));

        var character = _context?.State.Key.CharacterId ?? _state?.Key.CharacterId ?? 0;

        if (kind is ScriptLineKind.Error)
            ScriptLog.Erred(_log, character, ScriptName, text);
        else
            ScriptLog.Said(_log, character, ScriptName, text);
    }
}
