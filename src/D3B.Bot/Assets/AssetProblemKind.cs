namespace D3B.Bot.Assets;

/// <summary>Why a file of the project was not read, which the app says in its language (D356).</summary>
public enum AssetProblemKind
{
    /// <summary>A value is missing, or is not of its type.</summary>
    Content,

    /// <summary>The file holds no JSON object.</summary>
    NotJson,

    /// <summary>The kind the file says is not the one its extension names.</summary>
    WrongKind,

    /// <summary>The file says no format, or one newer than this version of D3B reads.</summary>
    Format,

    /// <summary>Its id is another asset's, as a file copied by hand.</summary>
    IdTaken,

    /// <summary>The system did not let the file be read.</summary>
    Unreadable,

    /// <summary>The file's extension names no kind of asset, as a file imported may.</summary>
    NotAsset,
}
