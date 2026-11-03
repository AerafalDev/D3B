namespace D3B.App.ViewModels.Bottom;

/// <summary>Where a problem comes from, which groups it in the problems' tab (D327).</summary>
internal enum ProblemOrigin
{
    /// <summary>The project: a file of its folder that does not read.</summary>
    Project,

    /// <summary>A character D3B knows: no profile, or one that is gone.</summary>
    Characters,

    /// <summary>The bots' host: why it did not start, its warnings and errors.</summary>
    Host,
}
