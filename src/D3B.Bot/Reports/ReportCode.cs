using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace D3B.Bot.Reports;

/// <summary>The code of a report, <c>D3B-XXXX-XX</c>, which the player gives on Discord and the maintainer finds the report by (D410).</summary>
public static partial class ReportCode
{
    // No 0, 1, I or O: a code is read aloud and typed back.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Makes a code at random.</summary>
    /// <returns>The code.</returns>
    public static string Create()
    {
        return $"D3B-{Characters(4)}-{Characters(2)}";
    }

    /// <summary>Tells whether a text is a code, which makes it safe to name a folder after.</summary>
    /// <param name="code">The text.</param>
    /// <returns>Whether it is a code.</returns>
    public static bool IsValid(string? code)
    {
        return code is not null && Pattern().IsMatch(code);
    }

    private static string Characters(int count)
    {
        return string.Create(count, 0, static (span, _) =>
        {
            for (var index = 0; index < span.Length; index++)
                span[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        });
    }

    [GeneratedRegex(@"^D3B-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
