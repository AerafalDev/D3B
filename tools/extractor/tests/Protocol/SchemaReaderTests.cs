using D3B.Extractor.Protocol;

namespace D3B.Extractor.Tests.Protocol;

public sealed class SchemaReaderTests
{
    private const string RepeatedField = "Google.Protobuf.Collections.RepeatedField`1";
    private const string MapField = "Google.Protobuf.Collections.MapField`2";

    private static readonly TypeShape.Property _parser = Getter("Google.Protobuf.MessageParser`1", Of("abc"));
    private static readonly TypeShape.Property _descriptor = Getter("Google.Protobuf.Reflection.MessageDescriptor");

    [Fact]
    public void AMessageIsItsFieldNumbersPairedWithItsPropertiesInOrder()
    {
        var schema = SchemaReader.Read([
            Message("abc", [1, 2, 4, 3], _parser, _descriptor, _descriptor, Value("System.Int64"), Value("System.String"), Value("System.Boolean"), Value("System.Single")),
        ]);

        var message = Assert.Single(schema.Messages);
        Assert.Equal("abc", message.Name);
        Assert.Equal([(1, "int64"), (2, "string"), (3, "float"), (4, "bool")], message.Fields.Select(field => (field.Number, field.Kind)));
        Assert.All(message.Fields, field => Assert.Null(field.Label));
    }

    [Fact]
    public void ListsMapsMessagesAndEnumsAreReadWithTheirTypes()
    {
        var schema = SchemaReader.Read([
            Message("abc", [1, 2, 3, 4, 5], Getter(RepeatedField, Of("System.Int32")), Value("abd"), Value("abc.abe.abf"), Getter(MapField, Of("System.String"), Of("abd")), Value("Google.Protobuf.WellKnownTypes.Any")),
            Message("abd", []),
            Enum("abc.abe.abf", [0, 1, 2]),
            Enum("unused", [0]),
        ]);

        var fields = schema.Messages.Single(message => message.Name == "abc").Fields;
        Assert.Equal(("int32", "repeated"), (fields[0].Kind, fields[0].Label));
        Assert.Equal(("message", "abd"), (fields[1].Kind, fields[1].Type));
        Assert.Equal(("enum", "abc.abe.abf"), (fields[2].Kind, fields[2].Type));
        Assert.Equal(("map", "string", "message", "abd"), (fields[3].Kind, fields[3].Key, fields[3].Value, fields[3].Type));
        Assert.Equal(("message", "google.protobuf.Any"), (fields[4].Kind, fields[4].Type));
        var used = Assert.Single(schema.Enums);
        Assert.Equal("abc.abe.abf", used.Name);
        Assert.Equal([0L, 1, 2], used.Values);
    }

    [Fact]
    public void AGetterOnlyBoolAfterAFieldMarksItsPresence()
    {
        var schema = SchemaReader.Read([
            Message("abc", [1, 2], Value("System.Int32"), Getter("System.Boolean"), Value("System.Boolean")),
        ]);

        var fields = Assert.Single(schema.Messages).Fields;
        Assert.Equal(("int32", "optional"), (fields[0].Kind, fields[0].Label));
        Assert.Equal(("bool", null), (fields[1].Kind, fields[1].Label));
    }

    [Fact]
    public void AOneofIsTheFieldsItsCaseEnumNumbers()
    {
        var schema = SchemaReader.Read([
            Message("abc", [2, 1, 3], Value("abd"), Value("System.Int32"), Getter("System.Boolean"), Value("abd"), Getter("abc.abe")),
            Message("abd", []),
            Enum("abc.abe", [0, 1, 3]),
        ]);

        var fields = schema.Messages.Single(message => message.Name == "abc").Fields;
        Assert.Equal([(1, "abc.abe", null), (2, null, null), (3, "abc.abe", null)], fields.Select(field => (field.Number, field.Oneof, field.Label)));
        Assert.Empty(schema.Enums);
    }

    [Fact]
    public void MessagesAndFieldsAreSortedWhateverTheirOrder()
    {
        var schema = SchemaReader.Read([Message("abd", [2, 1], Value("System.Int32"), Value("System.Int64")), Message("abc", [])]);

        Assert.Equal(["abc", "abd"], schema.Messages.Select(message => message.Name));
        Assert.Equal([1, 2], schema.Messages[1].Fields.Select(field => field.Number));
    }

    [Fact]
    public void ATypeTheReaderDoesNotKnowFails()
    {
        var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], Value("System.Decimal"))]));

        Assert.Contains("abc", error.Message, StringComparison.Ordinal);
        Assert.Contains("System.Decimal", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMessageNamingATypeOutsideTheSchemaFails()
    {
        var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], Value("abz"))]));

        Assert.Contains("of abz, which is no type", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MoreNumbersThanFieldsFail()
    {
        var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1, 2], Value("System.Int32"))]));

        Assert.Contains("2 field numbers and 1 fields", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANumberTwiceOrOutOfRangeFails()
    {
        Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1, 1], Value("System.Int32"), Value("System.Int32"))]));
        Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [0], Value("System.Int32"))]));
        Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1 << 29], Value("System.Int32"))]));
    }

    [Fact]
    public void ACaseEnumNumberingNoFieldFails()
    {
        var shapes = new[] { Message("abc", [1], Value("System.Int32"), Getter("abc.abe")), Enum("abc.abe", [0, 1, 2]) };

        Assert.Contains("names field 2", Assert.Throws<InvalidDataException>(() => SchemaReader.Read(shapes)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEnumInAOneofHasItsPresenceToo()
    {
        var schema = SchemaReader.Read([
            Message("abc", [1, 2], Value("abd"), Value("abc.abf"), Getter("System.Boolean"), Getter("abc.abe")),
            Message("abd", []),
            Enum("abc.abe", [0, 1, 2]),
            Enum("abc.abf", [0, 1]),
        ]);

        var fields = schema.Messages.Single(message => message.Name == "abc").Fields;
        Assert.Equal([(1, "message", "abc.abe"), (2, "enum", "abc.abe")], fields.Select(field => (field.Number, field.Kind, field.Oneof)));
    }

    [Fact]
    public void APresenceAfterAListFails()
    {
        Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], Getter(RepeatedField, Of("System.Int32")), Getter("System.Boolean"))]));
    }

    [Fact]
    public void APresenceWithNoFieldBeforeItFails()
    {
        Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [], Getter("System.Boolean"))]));
    }

    [Fact]
    public void AGetterOnlyScalarThatIsNoPresenceFails()
    {
        Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], Getter("System.Int32"))]));
    }

    [Fact]
    public void AListOrAMapInAOneofFails()
    {
        foreach (var collection in new[] { Getter(RepeatedField, Of("System.Int32")), Getter(MapField, Of("System.Int32"), Of("System.Int32")) })
        {
            var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], collection, Getter("abc.abe")), Enum("abc.abe", [0, 1])]));

            Assert.Contains("in the oneof abc.abe", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AScalarInAOneofWithoutItsPresenceFails()
    {
        var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], Value("System.Int32"), Getter("abc.abe")), Enum("abc.abe", [0, 1])]));

        Assert.Contains("without its presence", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoOneofsNamingOneFieldFail()
    {
        var shapes = new[] { Message("abc", [1], Value("abd"), Getter("abc.abe"), Getter("abc.abf")), Message("abd", []), Enum("abc.abe", [0, 1]), Enum("abc.abf", [0, 1]) };

        var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read(shapes));

        Assert.Contains("names field 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMapKeyedByAFloatBytesAnEnumOrAMessageFails()
    {
        foreach (var key in new[] { "System.Single", "Google.Protobuf.ByteString", "abd", "abc.abe" })
        {
            var shapes = new[] { Message("abc", [1], Getter(MapField, Of(key), Of("System.Int32"))), Message("abd", []), Enum("abc.abe", [0]) };

            var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read(shapes));

            Assert.Contains("a map keyed by", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void APresenceAfterAPresenceOrAMessageFails()
    {
        foreach (var before in new[] { new[] { Value("System.Int32"), Getter("System.Boolean") }, [Value("abd")] })
        {
            var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], [.. before, Getter("System.Boolean")]), Message("abd", [])]));

            Assert.Contains("presence that follows no scalar", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AGetterOnlyEnumNotDeclaredInTheMessageItselfFails()
    {
        foreach (var name in new[] { "abe", "abc.abd.abe" })
        {
            var error = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], Value("System.Int32"), Getter(name)), Enum(name, [0, 1])]));

            Assert.Contains("cannot be set", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AListOfNoTypeOrThatCanBeSetFails()
    {
        var untyped = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], Getter(RepeatedField))]));
        var settable = new TypeShape.Property { Type = Of(RepeatedField, Of("System.Int32")), HasSetter = true };
        var set = Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", [1], settable)]));

        Assert.Contains("no type the reader knows", untyped.Message, StringComparison.Ordinal);
        Assert.Contains("can be set", set.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHighestFieldNumberIsRead()
    {
        var schema = SchemaReader.Read([Message("abc", [(1 << 29) - 1], Value("System.Int32"))]);

        Assert.Equal((1 << 29) - 1, Assert.Single(Assert.Single(schema.Messages).Fields).Number);
    }

    [Fact]
    public void TheSameTypeNamedTwiceFails()
    {
        Assert.Throws<InvalidDataException>(() => SchemaReader.Read([Message("abc", []), Message("abc", [])]));
    }

    private static TypeRef Of(string name, params TypeRef[] arguments)
    {
        return new TypeRef { Name = name, Arguments = arguments };
    }

    private static TypeShape.Property Value(string type)
    {
        return new TypeShape.Property { Type = Of(type), HasSetter = true };
    }

    private static TypeShape.Property Getter(string type, params TypeRef[] arguments)
    {
        return new TypeShape.Property { Type = Of(type, arguments), HasSetter = false };
    }

    private static TypeShape Message(string name, long[] numbers, params TypeShape.Property[] properties)
    {
        return new TypeShape { Name = name, IsEnum = false, IsMessage = true, Literals = numbers, Properties = properties };
    }

    private static TypeShape Enum(string name, long[] values)
    {
        return new TypeShape { Name = name, IsEnum = true, IsMessage = false, Literals = values, Properties = [] };
    }
}
