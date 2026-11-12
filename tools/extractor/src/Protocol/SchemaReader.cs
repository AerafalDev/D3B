namespace D3B.Extractor.Protocol;

/// <summary>Reads the protocol's schema from the shapes of the client's protocol types, from the metadata alone (ADR 0012).</summary>
/// <remarks>
/// Google.Protobuf's generated C# declares, for each field in the order of the <c>.proto</c>, an <c>int</c> constant,
/// its number, and a property; a getter-only <c>bool</c> after a scalar is its presence (proto3 <c>optional</c>, or a
/// scalar or an enum in a oneof); a getter-only property typed by an enum nested in the message is a oneof's case, its non-zero
/// values its fields' numbers (inferred, the C# version's D40, <c>main:docs/client.md</c>, "Protocol code"; verified on
/// 3.7.13.17's <c>jow</c>, <c>jnz</c>, <c>hcn</c> and <c>hdm</c>). Anything else fails with what it met.
/// </remarks>
internal static class SchemaReader
{
    /// <summary>The highest field number protobuf allows.</summary>
    private const long MaxNumber = (1 << 29) - 1;

    private const string Repeated = "Google.Protobuf.Collections.RepeatedField`1";
    private const string Map = "Google.Protobuf.Collections.MapField`2";
    private const string Any = "Google.Protobuf.WellKnownTypes.Any";

    private static readonly Dictionary<string, string> _scalars = new(StringComparer.Ordinal)
    {
        ["System.Int32"] = "int32",
        ["System.Int64"] = "int64",
        ["System.UInt32"] = "uint32",
        ["System.UInt64"] = "uint64",
        ["System.Boolean"] = "bool",
        ["System.String"] = "string",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["Google.Protobuf.ByteString"] = "bytes",
    };

    /// <summary>Reads the messages among <paramref name="shapes"/> and the enums their fields hold.</summary>
    /// <exception cref="InvalidDataException">A shape is not as the reader knows them.</exception>
    public static ProtocolSchema Read(IEnumerable<TypeShape> shapes)
    {
        var types = new Dictionary<string, TypeShape>(StringComparer.Ordinal);

        foreach (var shape in shapes)
        {
            if (!types.TryAdd(shape.Name, shape))
            {
                throw new InvalidDataException($"The protocol's code declares {shape.Name} twice.");
            }
        }

        var held = new SortedSet<string>(StringComparer.Ordinal);
        var messages = types.Values
            .Where(type => type.IsMessage)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .Select(type => ReadMessage(type, types, held))
            .ToList();
        var enums = held.Select(name => new SchemaEnum { Name = name, Values = types[name].Literals }).ToList();

        return new ProtocolSchema { Messages = messages, Enums = enums };
    }

    private static SchemaMessage ReadMessage(TypeShape message, Dictionary<string, TypeShape> types, SortedSet<string> held)
    {
        var values = new List<(TypeShape.Property Property, bool Present)>();
        var cases = new List<TypeShape>();

        foreach (var property in message.Properties)
        {
            var name = property.Type.Name;

            if (name is "Google.Protobuf.MessageParser`1" or "Google.Protobuf.Reflection.MessageDescriptor")
            {
                continue;
            }

            if (!property.HasSetter && name is "System.Boolean")
            {
                if (values.Count is 0 || values[^1].Present || !IsScalar(values[^1].Property.Type, types))
                {
                    throw new InvalidDataException($"{message.Name} has a presence that follows no scalar field.");
                }

                values[^1] = (values[^1].Property, true);

                continue;
            }

            if (!property.HasSetter && types.TryGetValue(name, out var oneof) && oneof.IsEnum && IsNestedIn(name, message.Name))
            {
                cases.Add(oneof);

                continue;
            }

            if (property.HasSetter == name is Repeated or Map)
            {
                var can = property.HasSetter ? "can" : "cannot";

                throw new InvalidDataException($"{message.Name} has a property of {property.Type} that {can} be set, which is no field the reader knows.");
            }

            values.Add((property, false));
        }

        var numbers = message.Literals;

        if (numbers.Count != values.Count)
        {
            throw new InvalidDataException($"{message.Name} declares {numbers.Count} field numbers and {values.Count} fields.");
        }

        if (numbers.Any(number => number is < 1 or > MaxNumber) || numbers.Distinct().Count() != numbers.Count)
        {
            throw new InvalidDataException($"{message.Name} declares the field numbers {string.Join(", ", numbers)}, not each once from 1 to {MaxNumber}.");
        }

        var oneofs = new Dictionary<long, string>();

        foreach (var oneof in cases)
        {
            foreach (var number in oneof.Literals.Where(value => value is not 0))
            {
                if (!numbers.Contains(number) || !oneofs.TryAdd(number, oneof.Name))
                {
                    throw new InvalidDataException($"{oneof.Name}, a oneof of {message.Name}, names field {number}, which is not one of its own.");
                }
            }
        }

        var fields = numbers
            .Select((number, index) => ReadField(message, (int)number, values[index].Property.Type, values[index].Present, oneofs.GetValueOrDefault(number), types, held))
            .OrderBy(field => field.Number)
            .ToList();

        return new SchemaMessage { Name = message.Name, Fields = fields };
    }

    private static SchemaField ReadField(TypeShape message, int number, TypeRef type, bool present, string? oneof, Dictionary<string, TypeShape> types, SortedSet<string> held)
    {
        if (oneof is not null && type.Name is Repeated or Map)
        {
            throw new InvalidDataException($"{message.Name} has field {number}, of {type}, in the oneof {oneof}, which protobuf does not allow.");
        }

        if (type.Name is Repeated && type.Arguments.Count is 1)
        {
            var (kind, of) = KindOf(message, type.Arguments[0], types, held);

            return new SchemaField { Number = number, Kind = kind, Type = of, Label = "repeated" };
        }

        if (type.Name is Map && type.Arguments.Count is 2)
        {
            var (key, keyType) = KindOf(message, type.Arguments[0], types, held);
            var (value, valueType) = KindOf(message, type.Arguments[1], types, held);

            if (keyType is not null || key is "float" or "double" or "bytes")
            {
                throw new InvalidDataException($"{message.Name} has a map keyed by {type.Arguments[0]}, which protobuf does not allow.");
            }

            return new SchemaField { Number = number, Kind = "map", Key = key, Value = value, Type = valueType };
        }

        var (single, singleType) = KindOf(message, type, types, held);

        // The code tracks the presence of every scalar and enum in a oneof (verified, 3.7.13.17): one without it is
        // paired with the wrong property.
        if (oneof is not null && !present && single is not "message")
        {
            throw new InvalidDataException($"{message.Name} has field {number}, of {type}, in the oneof {oneof} without its presence.");
        }

        return new SchemaField
        {
            Number = number,
            Kind = single,
            Type = singleType,
            Label = present && oneof is null ? "optional" : null,
            Oneof = oneof,
        };
    }

    // A oneof's case enum is declared in its message itself, never in the `Types` class of its nested types.
    private static bool IsNestedIn(string name, string message)
    {
        return name.StartsWith(message + ".", StringComparison.Ordinal) && !name.AsSpan(message.Length + 1).Contains('.');
    }

    // A field the wire writes as one value: a scalar or an enum, whose presence the code may track.
    private static bool IsScalar(TypeRef type, Dictionary<string, TypeShape> types)
    {
        return type.Arguments.Count is 0 && (_scalars.ContainsKey(type.Name) || (types.TryGetValue(type.Name, out var shape) && shape.IsEnum));
    }

    private static (string Kind, string? Type) KindOf(TypeShape message, TypeRef type, Dictionary<string, TypeShape> types, SortedSet<string> held)
    {
        if (type.Arguments.Count is 0)
        {
            if (_scalars.TryGetValue(type.Name, out var scalar))
            {
                return (scalar, null);
            }

            if (type.Name is Any)
            {
                return ("message", "google.protobuf.Any");
            }

            if (types.TryGetValue(type.Name, out var shape) && (shape.IsMessage || shape.IsEnum))
            {
                if (shape.IsEnum)
                {
                    held.Add(shape.Name);
                }

                return (shape.IsEnum ? "enum" : "message", shape.Name);
            }
        }

        throw new InvalidDataException($"{message.Name} has a field of {type}, which is no type the reader knows.");
    }
}
