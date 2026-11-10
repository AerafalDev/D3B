namespace D3B.Bot.Reports;

/// <summary>Who a report's texts must not name: the character reported is told by its number, the account's other characters by nothing (D410).</summary>
public sealed class ReportIdentity
{
    // A one-letter name would hide words of the text, and a text that mentions it by chance is not worth that.
    private const int LeastNameLength = 2;

    private readonly (string Name, string Label)[] _names;

    /// <summary>Initializes a new instance of the <see cref="ReportIdentity"/> class.</summary>
    /// <param name="characterId">The id of the character reported.</param>
    /// <param name="name">Its name.</param>
    /// <param name="otherNames">The names of the account's other characters.</param>
    public ReportIdentity(long characterId, string name, IEnumerable<string> otherNames)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(otherNames);

        // The longest first, so that a name holding another is hidden whole.
        _names =
        [
            .. otherNames.Select(other => (Name: other, Label: "another character of the account"))
                .Append((Name: name, Label: $"character {characterId}"))
                .Where(pair => pair.Name.Length >= LeastNameLength)
                .OrderByDescending(pair => pair.Name.Length),
        ];
    }

    /// <summary>Replaces the names in a text, whatever their case.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with the character told by its number and the others by what they are.</returns>
    public string Hide(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var (name, label) in _names)
            text = text.Replace(name, label, StringComparison.OrdinalIgnoreCase);

        return text;
    }
}
