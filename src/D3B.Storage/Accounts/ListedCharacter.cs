namespace D3B.Storage.Accounts;

/// <summary>A character as the server lists or confirms it: its id, name, level, breed and sex (D99, D323).</summary>
/// <param name="Id">The character's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Level">Its level, if the server said.</param>
/// <param name="BreedId">Its breed's id in the client's table, if the server said.</param>
/// <param name="IsFemale">Whether it is female, if the server said.</param>
internal sealed record ListedCharacter(long Id, string Name, int? Level, int? BreedId = null, bool? IsFemale = null);
