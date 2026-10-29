namespace D3B.Scripting.Api;

/// <summary>The <c>log</c> namespace: what the script tells its console, which the bot's log keeps too.</summary>
internal static class LogNamespace
{
    public static ScriptNamespace Create(ScriptBinding binding)
    {
        return new ScriptNamespace("log", binding)
            .Add("info", call => Write(call, ScriptLineKind.Message))
            .Add("success", call => Write(call, ScriptLineKind.Success))
            .Add("error", call => Write(call, ScriptLineKind.Error));
    }

    /// <summary>Writes every argument, joined as <c>print</c> joins them.</summary>
    public static int Write(ScriptCall call, ScriptLineKind kind)
    {
        call.Binding.Write(kind, call.Joined());

        return call.Return();
    }
}
