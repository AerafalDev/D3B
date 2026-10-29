using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Scripting.Api;
using D3B.Scripting.Declarations;
using D3B.Scripting.Sandbox;
using Lua;

namespace D3B.Scripting;

/// <summary>An activity a Lua script writes: its hooks are the activity's, each given the <c>bot</c> (D102, D103, D105).</summary>
/// <remarks>
/// <para>
/// The script runs in a sandbox of its own (<see cref="ScriptSandbox"/>), one per start, as every activity is made for one
/// start (D93). Its first step runs the script's text, which defines its hooks, then gives its global <c>settings</c> the
/// values a profile or the defaults hold. Each step then calls <c>step(bot)</c>, chosen from the state as every activity's
/// step (D87); <c>false</c> ends the activity. The other hooks are optional, as <see cref="BotActivity"/>'s:
/// <c>isFull(bot)</c>, which reads and never acts; <c>unload(bot)</c>, which empties a full character and says whether it
/// did; <c>onInterrupted(reason)</c>, which answers <c>"stop"</c> to end the activity; <c>recover(bot, reason)</c>, run
/// after the default recovery.
/// </para>
/// <para>
/// The script's calls to the bot are the hook's behaviors, so an interruption cancels the call the script awaits, which
/// <c>pcall</c> cannot catch, measured (D103). The script's globals stay as they were, and the next step, once the bot is
/// recovered, starts again from the state. A first step cut short before the text ran whole runs it again in a new state.
/// A script's error ends the activity, which fails with a <see cref="ScriptException"/> naming the script's line, and the
/// app shows it.
/// </para>
/// </remarks>
public sealed class ScriptActivity : BotActivity, IDisposable
{
    private readonly LuaScript _script;
    private readonly ActivitySettings _declared;
    private readonly object? _settings;
    private ScriptBinding? _binding;
    private LuaTable? _bot;
    private bool _loaded;

    /// <summary>Gets the hooks a script defines, by name: <c>step</c>, then those it may.</summary>
    internal static IReadOnlyList<string> Hooks { get; } = ["step", "isFull", "unload", "onInterrupted", "recover"];

    /// <inheritdoc/>
    public override string Name => _script.Name;

    /// <summary>Gets what the script wrote, and the error that ended it.</summary>
    public ScriptConsole Console { get; } = new();

    /// <summary>Initializes a new instance of the <see cref="ScriptActivity"/> class, for a script with no setting.</summary>
    /// <param name="script">The script.</param>
    public ScriptActivity(LuaScript script)
        : this(script, ActivitySettings.None, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ScriptActivity"/> class.</summary>
    /// <param name="script">The script.</param>
    /// <param name="declared">The settings the script declares.</param>
    /// <param name="settings">Their values, a <see cref="SettingValues"/>, or <see langword="null"/> when it declares none.</param>
    public ScriptActivity(LuaScript script, ActivitySettings declared, object? settings)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(declared);

        _script = script;
        _declared = declared;
        _settings = settings;
    }

    /// <inheritdoc/>
    public override Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var loaded = _loaded;

        return HookAsync(context, true, async binding =>
        {
            // The run asks whether the character is full before each step (D87), but the first before the text ran: a
            // character full once it ran is unloaded before the script's first step.
            if (!loaded && IsFull(context.State))
                return true;

            binding.Enter(context);

            var step = binding.State.Environment["step"];

            if (step.Type is not LuaValueType.Function)
                throw new ScriptException($"{_script.Name}: the script defines no step(bot) function, which the bot calls for each step.");

            var results = await binding.State.CallAsync(step, [_bot!], cancellationToken).ConfigureAwait(false);

            return !(results.Length > 0 && results[0].TryRead<bool>(out var more) && !more);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public override bool IsFull(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!_loaded || _binding is not { } binding || binding.State.Environment["isFull"] is not { Type: LuaValueType.Function } isFull)
            return false;

        binding.EnterReading(state);

        try
        {
            return Truthy(CallReading(binding, isFull, _bot!));
        }
        catch (Exception exception) when (ScriptException.IsScriptError(exception))
        {
            throw Failed(state, exception);
        }
        finally
        {
            binding.Leave();
        }
    }

    /// <inheritdoc/>
    public override Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_loaded || _binding?.State.Environment["unload"] is not { Type: LuaValueType.Function })
            return base.UnloadAsync(context, cancellationToken);

        return HookAsync(context, false, async binding =>
        {
            var results = await binding.State.CallAsync(binding.State.Environment["unload"], [_bot!], cancellationToken).ConfigureAwait(false);

            return results.Length > 0 && Truthy(results[0]);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        if (!_loaded || _binding is not { } binding || binding.State.Environment["onInterrupted"] is not { Type: LuaValueType.Function } hook)
            return InterruptionResponse.Resume;

        binding.EnterReading(null);

        try
        {
            return CallReading(binding, hook, Outcomes.NameOf(reason)).TryRead<string>(out var answer) && answer == "stop"
                ? InterruptionResponse.Stop
                : InterruptionResponse.Resume;
        }
        catch (Exception exception) when (ScriptException.IsScriptError(exception))
        {
            throw Failed(null, exception);
        }
        finally
        {
            binding.Leave();
        }
    }

    /// <inheritdoc/>
    public override async Task RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        await base.RecoverAsync(context, reason, cancellationToken).ConfigureAwait(false);

        if (!_loaded || _binding?.State.Environment["recover"] is not { Type: LuaValueType.Function })
            return;

        await HookAsync(context, false, async binding =>
        {
            await binding.State.CallAsync(binding.State.Environment["recover"], [_bot!, Outcomes.NameOf(reason)], cancellationToken).ConfigureAwait(false);

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _binding?.State.Dispose();
        _binding = null;
        _bot = null;
    }

    private static bool Truthy(LuaValue value)
    {
        return value.Type is not LuaValueType.Nil && !(value.TryRead<bool>(out var flag) && !flag);
    }

    private static LuaValue CallReading(ScriptBinding binding, LuaValue hook, LuaValue argument)
    {
        // A hook that reads calls no action, which throws there: nothing it calls waits, so its call ends at once.
        var results = binding.State.CallAsync(hook, [argument]).AsTask().GetAwaiter().GetResult();

        return results.Length > 0 ? results[0] : LuaValue.Nil;
    }

    private async Task<T> HookAsync<T>(BotContext context, bool loads, Func<ScriptBinding, Task<T>> hook, CancellationToken cancellationToken)
    {
        try
        {
            var binding = _loaded && _binding is { } loaded ? loaded
                : loads ? await LoadAsync(context, cancellationToken).ConfigureAwait(false)
                : throw new InvalidOperationException("The script's text has not run.");

            binding.Enter(context);

            try
            {
                return await hook(binding).ConfigureAwait(false);
            }
            finally
            {
                binding.Leave();
            }
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested && exception is not OperationCanceledException)
        {
            // Whatever the script met once its hook was cancelled, such as a request that could not go, follows from the
            // cancellation: the activity is interrupted or stopped, not failed.
            throw new OperationCanceledException("The script's hook was cancelled.", exception, cancellationToken);
        }
        catch (Exception exception) when (exception is ScriptException || ScriptException.IsScriptError(exception))
        {
            throw Failed(context.State, exception);
        }
    }

    private ScriptException Failed(CharacterState? state, Exception exception)
    {
        var error = exception as ScriptException ?? ScriptException.From(exception);
        var binding = _binding;

        if (binding is not null)
        {
            binding.EnterReading(state);
            binding.Write(ScriptLineKind.Error, error.Message);
            binding.Leave();
        }
        else
        {
            Console.Write(new ScriptLine(TimeProvider.System.GetUtcNow(), ScriptLineKind.Error, error.Message));
        }

        return error;
    }

    private async Task<ScriptBinding> LoadAsync(BotContext context, CancellationToken cancellationToken)
    {
        // A text cut short left a state half made: the script starts over in a new one.
        Dispose();

        var binding = new ScriptBinding(_script.Name, Console, ScriptSandbox.Create(context.Time));

        _bot = ScriptApi.Register(binding);
        _binding = binding;
        binding.Enter(context);

        try
        {
            await binding.State.DoStringAsync(_script.Source, "@" + _script.Name, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            binding.Leave();
        }

        binding.State.Environment["settings"] = ScriptSettings.ToLua(_declared, _settings);
        _loaded = true;

        return binding;
    }
}
