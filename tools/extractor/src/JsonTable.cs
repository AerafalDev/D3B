using System.Text.Encodings.Web;
using System.Text.Json;
using D3B.Extractor.Texts;

namespace D3B.Extractor;

/// <summary>Writes a table of the data folder: a JSON array, one record per line (docs/data.md).</summary>
/// <remarks>One record per line keeps a table compact and lets a diff of two builds show the records that changed.</remarks>
internal static class JsonTable
{
    private static readonly JsonWriterOptions _options = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Writes the records, each as one object by <paramref name="write"/>.</summary>
    public static byte[] Write<T>(IEnumerable<T> records, Action<Utf8JsonWriter, T> write)
    {
        using var buffer = new MemoryStream();
        var first = true;

        buffer.WriteByte((byte)'[');

        foreach (var record in records)
        {
            buffer.Write(first ? "\n"u8 : ",\n"u8);
            first = false;

            using var json = new Utf8JsonWriter(buffer, _options);

            json.WriteStartObject();
            write(json, record);
            json.WriteEndObject();
        }

        buffer.Write(first ? "]\n"u8 : "\n]\n"u8);

        return buffer.ToArray();
    }

    /// <summary>Writes a text in its five languages, or <see langword="null"/> for none.</summary>
    public static void WriteText(Utf8JsonWriter json, string name, Text? text)
    {
        if (text is null)
        {
            json.WriteNull(name);

            return;
        }

        json.WriteStartObject(name);
        json.WriteString("fr", text.Fr);
        json.WriteString("en", text.En);
        json.WriteString("de", text.De);
        json.WriteString("es", text.Es);
        json.WriteString("pt", text.Pt);
        json.WriteEndObject();
    }
}
