using D3B.Bot.Localization;
using D3B.Bot.Settings;
using Lua;

namespace D3B.Scripting.Declarations;

/// <summary>A script's settings: the table it declares them in, onto D98's kinds, and their values as the script reads them (D107).</summary>
/// <remarks>
/// <para>
/// A script declares its settings as data, an entry per setting in the order the app shows them, so that the app reads
/// them without running the bot:
/// </para>
/// <code>
/// settings = {
///   { section = { fr = "Promenade", en = "Stroll" } },
///   { key = "walks", kind = "number", name = "Marches", min = 1, max = 100, default = 10, help = "..." },
///   { key = "job", kind = "job", name = "Métier", required = true },
///   { key = "resources", kind = "resources", name = "Ressources", within = "job", ordered = true },
/// }
/// </code>
/// <para>
/// Kinds: <c>flag</c>, <c>number</c>, <c>text</c>, <c>choice</c> and <c>choices</c> among the entry's <c>options</c>, and
/// the game's <c>job</c>, <c>area</c>, <c>subArea</c>, <c>resource</c>, one value or, with an <c>s</c>, several. A name, a
/// help, a section and an option's name are a string, or a table by language, <c>{ fr = "...", en = "..." }</c>. Once the
/// script's text ran, its global <c>settings</c> holds the values instead, by key: <c>settings.walks</c>.
/// </para>
/// </remarks>
internal static class ScriptSettings
{
    private const int DefaultTextLength = 100;

    private static readonly Dictionary<string, (SettingKind Kind, bool IsList)> _kinds = new(StringComparer.Ordinal)
    {
        ["flag"] = (SettingKind.Flag, false),
        ["number"] = (SettingKind.Number, false),
        ["text"] = (SettingKind.Text, false),
        ["choice"] = (SettingKind.Choice, false),
        ["choices"] = (SettingKind.Choice, true),
        ["job"] = (SettingKind.Job, false),
        ["jobs"] = (SettingKind.Job, true),
        ["area"] = (SettingKind.Area, false),
        ["areas"] = (SettingKind.Area, true),
        ["subArea"] = (SettingKind.SubArea, false),
        ["subAreas"] = (SettingKind.SubArea, true),
        ["resource"] = (SettingKind.Resource, false),
        ["resources"] = (SettingKind.Resource, true),
    };

    private static readonly HashSet<string> _fields = new(StringComparer.Ordinal)
    {
        "key", "kind", "name", "help", "default", "min", "max", "maxLength", "options", "required", "ordered", "within", "section",
    };

    /// <summary>Gets the kinds a script declares, by name.</summary>
    public static IReadOnlyCollection<string> Kinds => _kinds.Keys;

    /// <summary>Reads a script's declaration into D98's settings.</summary>
    /// <param name="scriptName">The script's name, which an error names.</param>
    /// <param name="declaration">The script's global <c>settings</c> once its text ran: nil for none.</param>
    /// <exception cref="ScriptException">The declaration does not read: what and where.</exception>
    public static ActivitySettings Read(string scriptName, LuaValue declaration)
    {
        if (declaration.Type is LuaValueType.Nil)
            return ActivitySettings.None;

        if (!declaration.TryRead<LuaTable>(out var entries))
            throw Error(scriptName, $"settings is a {declaration.TypeToString()}, where a table of settings was expected");

        var builder = new SettingValuesBuilder();

        for (var index = 1; index <= entries.ArrayLength; index++)
        {
            if (!entries[index].TryRead<LuaTable>(out var entry))
                throw Error(scriptName, $"settings[{index}] is a {entries[index].TypeToString()}, where a table was expected");

            try
            {
                Declare(builder, entry);
            }
            catch (ArgumentException exception)
            {
                throw Error(scriptName, $"settings[{index}]: {exception.Message.Split(" (Parameter", 2)[0]}");
            }
            catch (InvalidOperationException exception)
            {
                throw Error(scriptName, $"settings[{index}]: {exception.Message}");
            }
        }

        try
        {
            return builder.Build();
        }
        catch (InvalidOperationException exception)
        {
            throw Error(scriptName, exception.Message);
        }
    }

    /// <summary>The values a script reads, by key, as Lua holds them.</summary>
    public static LuaTable ToLua(ActivitySettings declared, object? values)
    {
        var table = new LuaTable();

        if (values is null)
            return table;

        foreach (var setting in declared.All)
        {
            if (setting.IsList)
            {
                var items = new LuaTable();
                var index = 1;

                foreach (var item in setting.GetValues(values))
                    items[index++] = LuaOf(item);

                table[setting.Key] = items;
            }
            else
            {
                table[setting.Key] = LuaOf(setting.GetValue(values));
            }
        }

        return table;
    }

    private static void Declare(SettingValuesBuilder builder, LuaTable entry)
    {
        foreach (var pair in entry)
        {
            if (!pair.Key.TryRead<string>(out var field) || !_fields.Contains(field))
                throw new ArgumentException($"a setting has no field {pair.Key}");
        }

        if (entry["section"].Type is not LuaValueType.Nil)
        {
            builder.Section(Text(entry["section"], "section"));

            return;
        }

        var key = Field<string>(entry, "key") ?? throw new ArgumentException("a setting has a key, a string");
        var kindName = Field<string>(entry, "kind") ?? throw new ArgumentException($"the setting {key} has a kind, a string");

        if (!_kinds.TryGetValue(kindName, out var kind))
            throw new ArgumentException($"the setting {key} has the kind {kindName}, which is none of {string.Join(", ", _kinds.Keys)}");

        var name = entry["name"].Type is LuaValueType.Nil ? LocalizedText.Invariant(key) : Text(entry["name"], $"{key}'s name");
        var value = entry["default"];
        SettingValueBuilder setting;

        switch (kind.Kind)
        {
            case SettingKind.Flag:
                setting = builder.Flag(key, name, value.Type is not LuaValueType.Nil && Read<bool>(value, $"{key}'s default is a boolean"));
                break;

            case SettingKind.Number:
                {
                    var minimum = Whole(entry["min"], $"{key}'s min");
                    var maximum = Whole(entry["max"], $"{key}'s max");

                    setting = builder.Number(key, name, minimum, maximum, value.Type is LuaValueType.Nil ? minimum : Whole(value, $"{key}'s default"));
                    break;
                }

            case SettingKind.Text:
                {
                    var length = entry["maxLength"].Type is LuaValueType.Nil ? DefaultTextLength : Whole(entry["maxLength"], $"{key}'s maxLength");

                    setting = builder.Text(key, name, length, value.Type is LuaValueType.Nil ? "" : Read<string>(value, $"{key}'s default is a string"));
                    break;
                }

            case SettingKind.Choice:
                {
                    var options = Options(entry, key);

                    setting = kind.IsList
                        ? builder.Choices(key, name, options, value.Type is LuaValueType.Nil ? [] : List(value, item => Read<string>(item, $"{key}'s default holds strings"), key))
                        : builder.Choice(key, name, options, value.Type is LuaValueType.Nil ? (string)options[0].Value : Read<string>(value, $"{key}'s default is a string"));
                    break;
                }

            default:
                setting = kind.IsList
                    ? builder.Games(key, kind.Kind, name, value.Type is LuaValueType.Nil ? [] : List(value, item => Whole(item, $"{key}'s default holds ids"), key))
                    : builder.Game(key, kind.Kind, name, value.Type is LuaValueType.Nil ? null : Whole(value, $"{key}'s default"));
                break;
        }

        if (entry["help"].Type is not LuaValueType.Nil)
            setting.Help(Text(entry["help"], $"{key}'s help"));

        if (Field<bool>(entry, "required"))
            setting.Required();

        if (Field<bool>(entry, "ordered"))
            setting.Ordered();

        if (Field<string>(entry, "within") is { } within)
            setting.Within(within);
    }

    private static List<SettingOption> Options(LuaTable entry, string key)
    {
        if (!entry["options"].TryRead<LuaTable>(out var table) || table.ArrayLength == 0)
            throw new ArgumentException($"the choice {key} has options, a list of values");

        var options = new List<SettingOption>();

        for (var index = 1; index <= table.ArrayLength; index++)
        {
            var option = table[index];

            if (option.TryRead<string>(out var value))
            {
                options.Add(new SettingOption(value, LocalizedText.Invariant(value)));
            }
            else if (option.TryRead<LuaTable>(out var named) && named["value"].TryRead<string>(out var text))
            {
                options.Add(new SettingOption(text, named["name"].Type is LuaValueType.Nil ? LocalizedText.Invariant(text) : Text(named["name"], $"{key}'s option {text}")));
            }
            else
            {
                throw new ArgumentException($"the choice {key}'s option {index} is a string, or a table with a value and a name");
            }
        }

        return options;
    }

    private static LocalizedText Text(LuaValue value, string what)
    {
        if (value.TryRead<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            return LocalizedText.Invariant(text);

        if (value.TryRead<LuaTable>(out var languages))
        {
            var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in languages)
            {
                if (!pair.Key.TryRead<string>(out var language) || !pair.Value.TryRead<string>(out var written))
                    throw new ArgumentException($"{what} is written by language, such as {{ fr = \"...\", en = \"...\" }}");

                texts[language] = written;
            }

            return LocalizedText.ByLanguage(texts);
        }

        throw new ArgumentException($"{what} is a text, or a table of texts by language");
    }

    private static T? Field<T>(LuaTable entry, string field)
    {
        var value = entry[field];

        return value.Type is LuaValueType.Nil ? default : Read<T>(value, $"the field {field} is a {(typeof(T) == typeof(bool) ? "boolean" : "string")}");
    }

    private static T Read<T>(LuaValue value, string what)
    {
        return value.TryRead<T>(out var read) ? read : throw new ArgumentException($"{what}, not a {value.TypeToString()}");
    }

    private static int Whole(LuaValue value, string what)
    {
        return value.TryRead<double>(out var number) && number == Math.Floor(number) && number is >= int.MinValue and <= int.MaxValue
            ? (int)number
            : throw new ArgumentException($"{what} is a whole number, not {value}");
    }

    private static List<T> List<T>(LuaValue value, Func<LuaValue, T> read, string key)
    {
        if (!value.TryRead<LuaTable>(out var table))
            throw new ArgumentException($"{key}'s default is a list");

        var items = new List<T>();

        for (var index = 1; index <= table.ArrayLength; index++)
            items.Add(read(table[index]));

        return items;
    }

    private static LuaValue LuaOf(object? value)
    {
        return value switch
        {
            null => LuaValue.Nil,
            bool flag => flag,
            int number => number,
            string text => text,
            _ => value.ToString() ?? "",
        };
    }

    private static ScriptException Error(string scriptName, string message)
    {
        return new ScriptException($"{scriptName}: {message.TrimEnd('.')}.");
    }
}
