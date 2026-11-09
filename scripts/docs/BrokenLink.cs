namespace D3B.Scripts.Docs;

/// <summary>A link of a Markdown file that leads nowhere.</summary>
/// <param name="File">The file the link is in, relative to the repository.</param>
/// <param name="Line">The link's line, from 1.</param>
/// <param name="Target">The link's target as written.</param>
/// <param name="Reason">Why it leads nowhere.</param>
internal sealed record BrokenLink(string File, int Line, string Target, string Reason);
