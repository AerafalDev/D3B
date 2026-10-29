using System.Text.RegularExpressions;
using D3B.Bot.Runtime;
using D3B.Scripting.Api;
using D3B.Scripting.Declarations;
using D3B.Scripting.Library;
using D3B.Scripting.Sandbox;
using Lua;

namespace D3B.Scripting.Tests.Library;

public sealed partial class ScriptDefinitionsTests
{
    private static readonly string _lua = ScriptDefinitions.Lua;

    private static List<string> Functions(LuaTable table)
    {
        var names = new List<string>();

        foreach (var pair in table)
        {
            if (pair.Value.Type is LuaValueType.Function)
                names.Add(pair.Key.Read<string>());
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    [GeneratedRegex(@"^function D3BBot:(\w+)\(", RegexOptions.Multiline)]
    private static partial Regex BotMethod();

    [GeneratedRegex(@"^function (\w+)\.(\w+)\(", RegexOptions.Multiline)]
    private static partial Regex NamespaceFunction();

    [Fact]
    public void TheDefinitionsDocumentTheWholeApiAndNothingElse()
    {
        using var state = ScriptSandbox.Create(TimeProvider.System);
        var binding = new ScriptBinding("test", new ScriptConsole(), state);
        var bot = ScriptApi.Register(binding);

        Assert.Equal(Functions(bot), BotMethod().Matches(_lua).Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal));

        foreach (var space in ScriptApi.Namespaces)
        {
            var documented = NamespaceFunction().Matches(_lua).Where(match => match.Groups[1].Value == space).Select(match => match.Groups[2].Value);

            Assert.Equal(Functions(state.Environment[space].Read<LuaTable>()), documented.Order(StringComparer.Ordinal));
        }

        foreach (var field in BotObject.Fields)
            Assert.Contains($"---@field {field} ", _lua, StringComparison.Ordinal);

        foreach (var hook in ScriptActivity.Hooks)
            Assert.Contains($"\nfunction {hook}(", _lua, StringComparison.Ordinal);

        foreach (var kind in ScriptSettings.Kinds)
            Assert.Contains($"---| \"{kind}\"", _lua, StringComparison.Ordinal);

        foreach (var reason in Enum.GetValues<Interruption>())
            Assert.Contains($"---| \"{Outcomes.NameOf(reason)}\"", _lua, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDefinitionsCompileAsLua()
    {
        using var state = ScriptSandbox.Create(TimeProvider.System);

        await state.DoStringAsync(_lua, "@d3b.lua", TestContext.Current.CancellationToken);
    }
}
