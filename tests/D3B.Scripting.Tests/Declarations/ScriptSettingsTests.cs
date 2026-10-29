using System.Globalization;
using D3B.Bot.Settings;
using D3B.Scripting.Declarations;
using D3B.Scripting.Sandbox;
using Lua;

namespace D3B.Scripting.Tests.Declarations;

public sealed class ScriptSettingsTests
{
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en-US");

    private static async Task<LuaValue> DeclarationAsync(string table)
    {
        using var state = ScriptSandbox.Create(TimeProvider.System);

        return (await state.DoStringAsync("return " + table, "@test.lua", TestContext.Current.CancellationToken))[0];
    }

    [Fact]
    public async Task ADeclarationReadsOntoTheKindsOfD98()
    {
        var settings = ScriptSettings.Read("test", await DeclarationAsync("""
            {
              { key = "bank", kind = "flag", name = "Banquer", default = true },
              { section = { fr = "Récolte", en = "Harvest" } },
              { key = "threshold", kind = "number", name = { fr = "Seuil", en = "Threshold" }, min = 50, max = 100, default = 90, help = "en %" },
              { key = "label", kind = "text", maxLength = 20 },
              { key = "city", kind = "choice", options = { "astrub", { value = "bonta", name = { fr = "Bonta la blanche", en = "Bonta" } } }, default = "bonta" },
              { key = "cities", kind = "choices", options = { "astrub", "bonta" }, default = { "astrub" }, ordered = true },
              { key = "job", kind = "job", required = true },
              { key = "resources", kind = "resources", within = "job", default = { 303, 312 } },
            }
            """));
        var all = settings.All;
        var values = Assert.IsType<SettingValues>(settings.CreateDefault());

        Assert.Equal(["bank", "threshold", "label", "city", "cities", "job", "resources"], all.Select(setting => setting.Key));
        Assert.Equal(
            [SettingKind.Flag, SettingKind.Number, SettingKind.Text, SettingKind.Choice, SettingKind.Choice, SettingKind.Job, SettingKind.Resource],
            all.Select(setting => setting.Kind));
        Assert.Equal([false, false, false, false, true, false, true], all.Select(setting => setting.IsList));
        Assert.Null(all[0].Section);
        Assert.Equal("Récolte", all[1].Section!.In(_french));
        Assert.Equal("Threshold", all[1].Name.In(_english));
        Assert.Equal((50, 100), (all[1].Minimum, all[1].Maximum));
        Assert.Equal("en %", all[1].Help!.In(_english));
        Assert.Equal(20, all[2].MaximumLength);
        Assert.Equal("label", all[2].Name.In(_english));
        Assert.Equal(["astrub", "bonta"], all[3].Options.Select(option => option.Value));
        Assert.Equal("Bonta la blanche", all[3].Options[1].Text.In(_french));
        Assert.True(all[4].IsOrdered);
        Assert.True(all[5].IsRequired);
        Assert.Same(all[5], all[6].Within);

        Assert.Equal(true, values["bank"]);
        Assert.Equal(90, values["threshold"]);
        Assert.Equal("", values["label"]);
        Assert.Equal("bonta", values["city"]);
        Assert.Equal(["astrub"], Assert.IsType<string[]>(values["cities"]));
        Assert.Null(values["job"]);
        Assert.Equal([303, 312], Assert.IsType<int[]>(values["resources"]));
        Assert.Equal([SettingProblemKind.Required], settings.Check(values).Select(problem => problem.Kind));
    }

    [Fact]
    public async Task AProfilesValuesReadAndWriteByKeyAndReachTheScriptAsATable()
    {
        var settings = ScriptSettings.Read("test", await DeclarationAsync("""
            {
              { key = "threshold", kind = "number", min = 50, max = 100, default = 90 },
              { key = "city", kind = "choice", options = { "astrub", "bonta" } },
              { key = "areas", kind = "areas" },
            }
            """));
        var reading = settings.Read("""{"threshold":75,"city":"bonta","areas":[1,2],"gone":3}""");

        Assert.Empty(reading.Reset);
        Assert.Equal("""{"threshold":75,"city":"bonta","areas":[1,2]}""", settings.Write(reading.Settings));
        Assert.Equal(["city"], settings.Read("""{"city":"brakmar"}""").Reset.Select(setting => setting.Key));

        var table = ScriptSettings.ToLua(settings, reading.Settings);

        Assert.Equal(75, table["threshold"].Read<int>());
        Assert.Equal("bonta", table["city"].Read<string>());
        Assert.Equal(2, table["areas"].Read<LuaTable>()[2].Read<int>());
    }

    [Fact]
    public async Task NoDeclarationIsNoSetting()
    {
        Assert.Same(ActivitySettings.None, ScriptSettings.Read("test", await DeclarationAsync("nil")));
    }

    [Theory]
    [InlineData("3", "test: settings is a number, where a table of settings was expected.")]
    [InlineData("{ 3 }", "test: settings[1] is a number, where a table was expected.")]
    [InlineData("{ { kind = \"flag\" } }", "test: settings[1]: a setting has a key, a string.")]
    [InlineData("{ { key = \"a\", kind = \"date\" } }", "test: settings[1]: the setting a has the kind date, which is none of flag, number, text, choice, choices, job, jobs, area, areas, subArea, subAreas, resource, resources.")]
    [InlineData("{ { key = \"a\", kind = \"flag\", colour = 1 } }", "test: settings[1]: a setting has no field colour.")]
    [InlineData("{ { key = \"a\", kind = \"number\", min = 1, max = 5, default = 9 } }", "test: The default settings do not hold: a (OutOfRange).")]
    [InlineData("{ { key = \"a\", kind = \"number\", min = 1.5, max = 5 } }", "test: settings[1]: a's min is a whole number, not 1.5.")]
    [InlineData("{ { key = \"a\", kind = \"flag\", default = \"yes\" } }", "test: settings[1]: a's default is a boolean, not a string.")]
    [InlineData("{ { key = \"a\", kind = \"choice\" } }", "test: settings[1]: the choice a has options, a list of values.")]
    [InlineData("{ { key = \"a\", kind = \"choice\", options = { \"x\" }, default = \"y\" } }", "test: The default settings do not hold: a (Unknown).")]
    [InlineData("{ { key = \"a\", kind = \"flag\" }, { key = \"a\", kind = \"flag\" } }", "test: settings[2]: The setting a is declared twice.")]
    [InlineData("{ { key = \"r\", kind = \"resource\", within = \"j\" } }", "test: settings[1]: The setting j is not declared before the one sought within it.")]
    [InlineData("{ { key = \"a\", kind = \"flag\", required = true } }", "test: settings[1]: The setting a always has a value: it cannot be required.")]
    public async Task ADeclarationThatDoesNotReadSaysWhatAndWhere(string table, string message)
    {
        var declaration = await DeclarationAsync(table);

        Assert.Equal(message, Assert.Throws<ScriptException>(() => ScriptSettings.Read("test", declaration)).Message);
    }
}
