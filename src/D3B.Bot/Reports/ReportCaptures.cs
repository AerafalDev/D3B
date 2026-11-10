using D3B.Network.Captures;

namespace D3B.Bot.Reports;

/// <summary>Finds the capture of a game connection: the relay names a capture after when it started and the connection's id (D37).</summary>
public static class ReportCaptures
{
    /// <summary>Gets the capture of a connection.</summary>
    /// <param name="directory">The folder of the game's captures.</param>
    /// <param name="connectionId">The id of the connection, as a bot keeps it.</param>
    /// <returns>The capture's path, or null when there is none: the folder is missing, the id is none, or the capture was swept.</returns>
    public static string? Find(string directory, string? connectionId)
    {
        ArgumentNullException.ThrowIfNull(directory);

        // The id is a file name's tail: nothing that could mean another file, a folder or a pattern.
        if (string.IsNullOrEmpty(connectionId) || connectionId.Any(character => !char.IsAsciiLetterOrDigit(character)) || !Directory.Exists(directory))
            return null;

        return Directory.EnumerateFiles(directory, "*-" + connectionId + CaptureWriter.FileExtension).OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault();
    }
}
