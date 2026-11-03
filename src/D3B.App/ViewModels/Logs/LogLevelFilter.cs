namespace D3B.App.ViewModels.Logs;

/// <summary>Which lines the journal shows, by their level (D111, D327).</summary>
internal enum LogLevelFilter
{
    /// <summary>Every line.</summary>
    All,

    /// <summary>The lines that are no problem: what was done and what the bot met.</summary>
    Infos,

    /// <summary>Warnings: an action that did not end as asked, a warning of the host.</summary>
    Warnings,

    /// <summary>Errors: an activity that failed, an error of the host.</summary>
    Errors,
}
