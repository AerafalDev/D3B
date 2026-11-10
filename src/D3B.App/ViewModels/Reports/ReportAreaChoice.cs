using D3B.Bot.Reports;

namespace D3B.App.ViewModels.Reports;

/// <summary>An area of a report as the dialog offers it.</summary>
/// <param name="Area">The area.</param>
/// <param name="Label">Its name.</param>
/// <param name="Detail">What it covers.</param>
internal sealed record ReportAreaChoice(ReportArea Area, string Label, string Detail);
