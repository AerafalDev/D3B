namespace D3B.App.ViewModels.Reports;

/// <summary>The steps of the report dialog (D410).</summary>
internal enum ReportStep
{
    /// <summary>The form: the area, the character and the message.</summary>
    Form,

    /// <summary>The report is being prepared: the capture cleaned, the texts written.</summary>
    Preparing,

    /// <summary>The preview, where each part is read and can be unchecked.</summary>
    Preview,

    /// <summary>The report is written.</summary>
    Done,
}
