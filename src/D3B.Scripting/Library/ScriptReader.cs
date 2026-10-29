using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Scripting.Api;
using D3B.Scripting.Declarations;
using D3B.Scripting.Sandbox;
using Lua;

namespace D3B.Scripting.Library;

/// <summary>Reads a script without a bot: runs its text in its sandbox, finds its hooks and its settings, and makes it an activity (D108).</summary>
/// <remarks>
/// A script's text defines its hooks and declares its settings; it cannot act, the <c>bot</c> reaching only its hooks, so
/// the library runs it to read what it declares, as the app lists the scripts. A text that runs on, such as a loop, is
/// stopped after <see cref="Timeout"/>. A script that does not read is still listed, as an activity whose start fails
/// with its error: the app says why, where the owner looks for it.
/// </remarks>
internal static class ScriptReader
{
    /// <summary>The longest a script's text runs while it is read.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <summary>Reads a script into an activity.</summary>
    /// <param name="id">The activity's id.</param>
    /// <param name="script">The script, named as the app shows it.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    /// <returns>The activity, and the script's error when it does not read.</returns>
    public static async Task<(ActivityDefinition Activity, ScriptException? Error)> ReadAsync(string id, LuaScript script, CancellationToken cancellationToken)
    {
        var name = LocalizedText.Invariant(script.Name);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var state = ScriptSandbox.Create(TimeProvider.System);
            var binding = new ScriptBinding(script.Name, new ScriptConsole(), state);

            ScriptApi.Register(binding);
            timeout.CancelAfter(Timeout);
            await state.DoStringAsync(script.Source, "@" + script.Name, timeout.Token).ConfigureAwait(false);

            if (state.Environment["step"].Type is not LuaValueType.Function)
                throw new ScriptException($"{script.Name}: the script defines no step(bot) function, which the bot calls for each step.");

            var settings = ScriptSettings.Read(script.Name, state.Environment["settings"]);

            return (new ActivityDefinition(id, name, settings, (_, values) => new ScriptActivity(script, settings, values)), null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Broken(id, name, new ScriptException($"{script.Name}: its text ran more than {Timeout.TotalSeconds} s; a script's text defines its hooks and settings, then ends."));
        }
        catch (Exception exception) when (exception is ScriptException || ScriptException.IsScriptError(exception))
        {
            return Broken(id, name, exception as ScriptException ?? ScriptException.From(exception));
        }
    }

    private static (ActivityDefinition, ScriptException) Broken(string id, LocalizedText name, ScriptException error)
    {
        return (new ActivityDefinition(id, name, _ => throw error), error);
    }
}
