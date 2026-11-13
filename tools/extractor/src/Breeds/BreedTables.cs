using System.Globalization;
using D3B.Extractor.Texts;
using D3B.Extractor.Unity;

namespace D3B.Extractor.Breeds;

/// <summary>The breeds of the data folder: <c>breeds.json</c> and each breed's head, <c>heads/&lt;breed&gt;-&lt;sex&gt;.png</c>.</summary>
/// <remarks>
/// <c>BreedData</c> names a breed by its <c>shortNameId</c>, a string holding a text's number, its only name on 3.7.13.17;
/// the client draws each breed's head for each sex, 0 the male and 1 the female (<c>artifacts/studies/breeds.md</c>).
/// </remarks>
internal static class BreedTables
{
    /// <summary>The sexes a head is drawn for: 0 the male, 1 the female, as the client's look and its heads' names hold them.</summary>
    public static readonly IReadOnlyList<int> Sexes = [0, 1];

    /// <summary>Reads the breeds and their heads from the client and writes them into the folder.</summary>
    /// <exception cref="InvalidDataException">The client's breeds or heads are not what the extractor measured.</exception>
    public static void Extract(ClientInstall client, TextFiles texts, DataFolder folder, TextWriter output)
    {
        var breeds = ReadBreeds(DataTable.ReadById(client, "breeds"), texts);
        var heads = HeadPictures.Read(client);

        Check(breeds, heads.Keys);
        folder.Write("breeds.json", Write(breeds));

        foreach (var ((breed, sex), png) in heads)
        {
            folder.Write(PathOf(breed, sex), png);
        }

        output.WriteLine($"Breeds: {breeds.Count:N0} breeds, {heads.Count:N0} heads of {HeadPictures.Size} px, {heads.Values.Sum(png => (long)png.Length):N0} bytes");
    }

    /// <exception cref="InvalidDataException">A breed's name is not a text's number, or names no text.</exception>
    public static IReadOnlyList<Breed> ReadBreeds(IEnumerable<Fields> records, TextFiles texts)
    {
        return
        [
            .. records
                .Select(record =>
                {
                    var id = record.Int32("id");

                    return new Breed { Id = id, Name = texts.Name(NameId(id, record.String("shortNameId"))) ?? throw new InvalidDataException($"Breed {id} names text 0.") };
                })
                .OrderBy(breed => breed.Id),
        ];
    }

    /// <summary>Reads a breed's <c>shortNameId</c>, a string that holds a text's number.</summary>
    /// <exception cref="InvalidDataException">The string is not a number.</exception>
    public static int NameId(int breed, string value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidDataException($"Breed {breed} names text \"{value}\", which is not a text's number.");
    }

    /// <summary>Checks that every breed has a head for each sex, and that no head is of another breed.</summary>
    /// <exception cref="InvalidDataException">A breed lacks a head, or a head is of no breed.</exception>
    public static void Check(IReadOnlyList<Breed> breeds, IEnumerable<(int Breed, int Sex)> heads)
    {
        var drawn = heads.ToHashSet();
        var expected = breeds.SelectMany(breed => Sexes.Select(sex => (breed.Id, sex))).ToHashSet();

        foreach (var (breed, sex) in expected.Order())
        {
            if (!drawn.Contains((breed, sex)))
            {
                throw new InvalidDataException($"Breed {breed} has no head for sex {sex}.");
            }
        }

        foreach (var (breed, sex) in drawn.Order())
        {
            if (!expected.Contains((breed, sex)))
            {
                throw new InvalidDataException($"The client draws a head of sex {sex} for breed {breed}, which its table does not hold.");
            }
        }
    }

    /// <summary>The path of a breed's head in the data folder.</summary>
    public static string PathOf(int breed, int sex)
    {
        return string.Create(CultureInfo.InvariantCulture, $"heads/{breed}-{sex}.png");
    }

    public static byte[] Write(IEnumerable<Breed> breeds)
    {
        return JsonTable.Write(breeds, (json, breed) =>
        {
            json.WriteNumber("id", breed.Id);
            JsonTable.WriteText(json, "name", breed.Name);
        });
    }
}
